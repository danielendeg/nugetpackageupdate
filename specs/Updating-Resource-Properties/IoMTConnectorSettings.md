This document outlines how IoMT Connector deployments update FHIR Server settings to allow read and write access.  

[[_TOC_]]

# Business Justification

When customers choose to add IoMT Connectors to their Azure API for FHIR instance, the resources backing the connector need to be provisioned and the IoMT Connector needs access to read and write data to the FHIR Server. Rather than re-provisioning the Azure API for FHIR resources and adding environment variables that would enable authentication/authorization, we are using the internal settings API to send new auth settings to the FHIR server. This will significantly reduce the amount of time that it takes to add/remove Connectors.

# Scenarios

As an "Azure Api for Fhir" service user, I want to add an IoMT Connector.

# Design

When a provisioning request is received for an IoMT Connector:

## Provisioning

1. An IomtConnectorOperationDocument is created
2. The ResourceProviderWorker picks up the document and deploys the required resources
3. The provisioning command calls the UpdateFhirServerSettings() API to add the auth settings for the Connector

## De-provisioning

1. An IomtConnectorOperationDocument is created
2. The ResourceProviderWorker picks up the document and removes the resources
3. The provisioning command calls the UpdateFhirServerSettings() API to remove the auth settings for the Connector

# Test Strategy

e2e Tests:

1. Provision a test account
2. Add an IoMT Connector
3. Verify the provisioning request completed successfully
4. Upload test mapping files
5. Generate a connection string
6. Send synthetic IoMT data to the Connector
7. Verify Observation resources are created
