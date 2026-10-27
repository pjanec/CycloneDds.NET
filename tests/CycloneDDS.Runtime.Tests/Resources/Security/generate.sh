#!/usr/bin/env bash
# Regenerates the DDS Security test fixtures used by DdsSecurityTests.
# TEST-ONLY material: the private keys are public by being committed here.
# Certificates are valid for 100 years so the fixtures do not expire.
set -euo pipefail
cd "$(dirname "$0")"
DAYS=36500

# One CA serves as both Identity CA and Permissions CA.
openssl req -x509 -newkey rsa:2048 -nodes -sha256 -days $DAYS \
  -keyout ca_key.pem -out ca_cert.pem -subj "/C=CZ/O=CycloneDDS.NET Test/CN=Test CA"

# Participant identity, signed by the CA.
openssl req -newkey rsa:2048 -nodes -sha256 \
  -keyout identity_key.pem -out identity.csr -subj "/C=CZ/O=CycloneDDS.NET Test/CN=participant"
openssl x509 -req -in identity.csr -CA ca_cert.pem -CAkey ca_key.pem -CAcreateserial \
  -days $DAYS -sha256 -out identity_cert.pem
rm -f identity.csr ca_cert.srl

# Governance and permissions documents, S/MIME-signed by the Permissions CA.
openssl smime -sign -in governance.xml -text -out governance.p7s -signer ca_cert.pem -inkey ca_key.pem
openssl smime -sign -in permissions.xml -text -out permissions.p7s -signer ca_cert.pem -inkey ca_key.pem
