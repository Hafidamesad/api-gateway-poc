Phase 1 ✅ PKI (Completed)

✔ Root CA
✔ Gateway private key
✔ Gateway certificate
✔ Service private key
✔ Service certificate
✔ Certificate verification
✔ gateway.p12
✔ service.p12
✔ truststore.p12

Phase 2
Integrate PKI into the application

This is where your certificates stop being files and start protecting communications.

Our objective is:

Browser
    │
 HTTPS
    │
Gateway
    │
mTLS
    │
Backend Service

At the moment, you only own the certificates.

Now we teach Spring Boot to use them.

