# Fhir Server on AKS

**STATUS: Work In Progress.**

[[_TOC_]]
# Background
The purpose of this document is to detail the design and changes for setting up Fhir Server on AKS.

# High Level Architecture
![Integration Architecture](./.images/k8s-arch.jpg)

# Container Image
## Prototype phase
[Fhir web project](https://microsofthealth.visualstudio.com/Health/_git/workspace-platform?version=GBpersonal/petyag/migration&path=/fhir/fhirservice) composes the right settings, services and middleware for PaaS.

It consumes packages from the OSS repo published to nuget feed [here](https://microsofthealthoss.visualstudio.com/FhirServer/_packaging?_a=feed&feed=Public).

The container image is built locally using the [docker file](https://microsofthealth.visualstudio.com/Health/_git/workspace-platform?version=GBpersonal/petyag/migration&path=/fhir/build/docker/dockerfile.fhirservice) and pushed to azure container registry 'workspaceplatform.azurecr.io'.

## Next phase
- Enable all other settings, services and middleware for [Fhir web project](https://microsofthealth.visualstudio.com/Health/_git/workspace-platform?version=GBpersonal/petyag/migration&path=/fhir/fhirservice) so we can achieve feature parity with Fhir Server implementation in health-paas repo. Some of these are:
  - Ifx and Shoebox Auditing
  - Authentication and Authorization
  - RBAC
  - Telemetry
  - Export
  - Reindex
- Publish all dependencies to internal packages in health-paas to an internal feed so that Fhir web project can consume latest changes.
- Update Fhir service [docker file](https://microsofthealth.visualstudio.com/Health/_git/workspace-platform?version=GBpersonal/petyag/migration&path=/fhir/build/docker/dockerfile.fhirservice) and Fhir Web Project to support different Fhir versions.
- Implement a CI pipeline to build Fhir container images and push them to an Azure container registry.

# Fhir resource in k8s
We will extend the Kubernetes API's with a custom resource `Fhir` to represent the Fhir service. We will use the kubebuilder and go lang to build the custom resource.

Fhir resource type can be defined in `workspace-platform/fhir/fhiroperator/api/v1alpha1/fhir_types.go`

Fhir controller will handle the actions on the Fhir resource and it will be responsible for creating or updating the deployment of Fhir service instance. It can be defined in `workspace-platform/fhir/fhiroperator/controllers/fhir_controller.go`

A new instance of Fhir resource will create
- `Azure Managed Identity` related resource instances
    - `AzureIdentity` represents a managed identity with clientId and resourceId.
    - `AzureIdentityBinding` represents a binding of a azureidentity
- `Ingress` with domain, service and port mapping.
- `Service` to map internal clusterIP and port.
- `Deployment` that sets the deployment strategy, replica sets and pod image. It also has the azureIdentityBinding label.

# Resource Provision
Resource Provision module includes the libraries for provisioning and de-provisioning the resources for the Fhir service instances.

## Package Structure

**Status: The package(namespace) structure should be delineated in the diagram. The model of the CRD API contract details will tend to update.**

![Diagram](./.images/provision-uml-design.jpg)

## Source Structure
```
workspace-platform/fhir/provision

.
├── Microsoft.Health.Cloud.Fhir.Provision
│   ├── K8s\Api\V1Alpha1\Models   // POCOs for K8s Service model 
│   └── Service
│       ├── Models      // POCOs for modeling Fhir Server Properties and Configuration
│       └── Providers   // Resource Providers
│
└── Microsoft.Health.Cloud.Fhir.Provision.Console //  Console App example 
```

# Resource Deprovision