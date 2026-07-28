#!/bin/bash
export MSYS_NO_PATHCONV=1
export MSYS2_ARG_CONV_EXCL="*"
set -e

PROJECT_ROOT="$(pwd)"
OUT="$PROJECT_ROOT/security/generated-pki"

CA_DIR="$OUT/root-ca"
GW_DIR="$OUT/gateway"
BE_DIR="$OUT/services"

mkdir -p "$CA_DIR"
mkdir -p "$GW_DIR"
mkdir -p "$BE_DIR"

PASS="changeit"

echo "=== Generating Root CA ==="

openssl ecparam -genkey -name prime256v1 -noout -out "$CA_DIR/ca.key"

openssl req \
-x509 \
-new \
-key "$CA_DIR/ca.key" \
-sha256 \
-days 3650 \
-subj '//C=MA/ST=Souss-Massa/L=Agadir/O=Netwaciila/CN=Netwaciila Root CA'
-out "$CA_DIR/ca.crt"
