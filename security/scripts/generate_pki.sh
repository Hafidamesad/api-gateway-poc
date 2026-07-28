#!/bin/bash
# ============================================================
# Génération PKI interne simplifiée - api-gateway-poc
# CA racine + certs Gateway (Zone1 serveur, Zone2 client)
# + certs Backends (Zone2 serveur : Etudiants/Transport/Finance/RH)
# Sortie : PKCS12 (.p12) prêts pour Spring Boot / .NET Kestrel

# ============================================================
export MSYS_NO_PATHCONV=1
export MSYS2_ARG_CONV_EXCL="*"

set -e

OUT="$HOME/edu-simulated-backends/security/generated-pki"
CA_DIR=$OUT/ca
GW_DIR=$OUT/gateway
BE_DIR=$OUT/backends
TS_DIR=$OUT/truststore

# Create output directories
mkdir -p "$CA_DIR"
mkdir -p "$GW_DIR"
mkdir -p "$BE_DIR"
mkdir -p "$TS_DIR"

# Mots de passe (à déplacer en secrets Docker en prod - cf. cahier des charges Axe transverse)
CA_PASS="changeit"
GW_PASS="changeit"
BE_PASS="changeit"
TS_PASS="changeit"

DAYS=825   # ~ durée de vie recommandée pour certs feuille
CA_DAYS=3650

echo "== 1. Génération de la CA racine (ECDSA P-256) =="
# Choix ECDSA : handshake plus rapide / moins de CPU que RSA, ce qui compte
# directement pour le SLA de latence <= 1.3s du cahier des charges.
openssl ecparam -name prime256v1 -genkey -noout -out $CA_DIR/ca.key
openssl req -x509 -new -key $CA_DIR/ca.key -sha256 -days $CA_DAYS \
  -subj "/C=MA/O=NetWaciila/OU=Internal-PKI/CN=NetWaciila-Root-CA" \
  -out $CA_DIR/ca.crt

echo "== 2. Certificat serveur Gateway (Zone 1 : Client -> Gateway, TLS standard) =="
openssl ecparam -name prime256v1 -genkey -noout -out $GW_DIR/gateway-server.key
openssl req -new -key $GW_DIR/gateway-server.key \
  -subj "/C=MA/O=NetWaciila/OU=Gateway/CN=gateway.netwaciila.local" \
  -out $GW_DIR/gateway-server.csr
openssl x509 -req -in $GW_DIR/gateway-server.csr -CA $CA_DIR/ca.crt -CAkey $CA_DIR/ca.key \
  -CAcreateserial -days $DAYS -sha256 \
  -extfile <(printf "subjectAltName=DNS:gateway.netwaciila.local,DNS:localhost,IP:127.0.0.1\nextendedKeyUsage=serverAuth") \
  -out $GW_DIR/gateway-server.crt

echo "== 3. Certificat CLIENT Gateway (Zone 2 : Gateway -> Backends, mTLS sortant) =="
openssl ecparam -name prime256v1 -genkey -noout -out $GW_DIR/gateway-client.key
openssl req -new -key $GW_DIR/gateway-client.key \
  -subj "/C=MA/O=NetWaciila/OU=Gateway/CN=gateway-client" \
  -out $GW_DIR/gateway-client.csr
openssl x509 -req -in $GW_DIR/gateway-client.csr -CA $CA_DIR/ca.crt -CAkey $CA_DIR/ca.key \
  -CAcreateserial -days $DAYS -sha256 \
  -extfile <(printf "extendedKeyUsage=clientAuth") \
  -out $GW_DIR/gateway-client.crt

echo "== 4. Certificats serveur pour chaque backend (Zone 2 entrant côté backend) =="
for svc in etudiants transport academique finance rhpaie; do
  openssl ecparam -name prime256v1 -genkey -noout -out $BE_DIR/$svc.key
  openssl req -new -key $BE_DIR/$svc.key \
    -subj "/C=MA/O=NetWaciila/OU=Backend/CN=$svc.backend.local" \
    -out $BE_DIR/$svc.csr
  openssl x509 -req -in $BE_DIR/$svc.csr -CA $CA_DIR/ca.crt -CAkey $CA_DIR/ca.key \
    -CAcreateserial -days $DAYS -sha256 \
    -extfile <(printf "subjectAltName=DNS:%s.backend.local,DNS:localhost,IP:127.0.0.1\nextendedKeyUsage=serverAuth" "$svc") \
    -out $BE_DIR/$svc.crt
  openssl pkcs12 -export \
    -in $BE_DIR/$svc.crt -inkey $BE_DIR/$svc.key -certfile $CA_DIR/ca.crt \
    -name "$svc" -out $BE_DIR/$svc.p12 -passout pass:$BE_PASS
done

echo "== 5. Packaging PKCS12 =="
# Keystore serveur Gateway (Zone 1)
openssl pkcs12 -export \
  -in $GW_DIR/gateway-server.crt -inkey $GW_DIR/gateway-server.key -certfile $CA_DIR/ca.crt \
  -name "gateway-server" -out $GW_DIR/gateway-server.p12 -passout pass:$GW_PASS

# Keystore client Gateway (Zone 2)
openssl pkcs12 -export \
  -in $GW_DIR/gateway-client.crt -inkey $GW_DIR/gateway-client.key -certfile $CA_DIR/ca.crt \
  -name "gateway-client" -out $GW_DIR/gateway-client.p12 -passout pass:$GW_PASS

# Truststore commun (contient uniquement la CA) - utilisé par la Gateway pour
# valider les certs serveur des backends, et à distribuer aux backends .NET
# pour valider le cert client de la Gateway.
keytool -importcert -noprompt -alias netwaciila-ca \
  -file $CA_DIR/ca.crt -keystore $TS_DIR/truststore.p12 -storetype PKCS12 \
  -storepass $TS_PASS

echo "== Terminé =="
echo "CA        : $CA_DIR/ca.crt (+ ca.key à garder secrète, hors repo)"
echo "Gateway   : $GW_DIR/gateway-server.p12 (Zone1) / gateway-client.p12 (Zone2)"
echo "Backends  : $BE_DIR/{etudiants,transport,finance,rhpaie}.p12"
echo "Truststore: $TS_DIR/truststore.p12"
echo ""
echo "Mots de passe (DEMO uniquement - à mettre en secrets Docker) :"
echo "  CA_PASS=$CA_PASS  GW_PASS=$GW_PASS  BE_PASS=$BE_PASS  TS_PASS=$TS_PASS"
