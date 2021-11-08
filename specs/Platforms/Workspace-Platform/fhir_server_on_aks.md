# Fhir Server on AKS

**STATUS: Work In Progress.**

Containers are a good way to bundle and run applications. In a production environment, the containers that run the applications need to be managed and ensure that there is no downtime. 
That's how Kubernetes comes to the rescue! Kubernetes provides a framework to run distributed systems resiliently. It takes care of scaling and failover for an application, provides deployment patterns, and more. Since Kubernetes operates at the container level rather than at the hardware level, it provides some generally applicable features common to PaaS offerings, such as deployment, scaling, load balancing, and lets users integrate their logging, monitoring, and alerting solutions.

Azure Kubernetes Service (AKS) is a hosted Kubernetes service on Azure. It simplifies deploying a managed Kubernetes cluster in Azure by offloading the operational overhead to Azure.

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

## Concepts
- Kind: The type of object that you'd like to create in Kubernetes. For example, a Pod, Deployment or Replica Set.
- Resource: A use of a particular Kind. For example, one or more instanciation of Kind __Pod__ may be created.
- Spec: The desired state of a resource
## Prototype phase
In K8s, there are the concepts of owners and dependents. It's very useful for K8s to perform GC and other operations. In our prototype design, for the Fhir Server, resource Kind is a Deployment, and then a ReplicaSet, then the Pod(s) and potentially along with other resources in descendant order.

In the example below, you can spot the ownership relationship by looking at the "uid" of current object and the "ownerReferences.uid" of its owner.

The Fhir Deployment object:
```json
{
    "kind": "Deployment",
    "apiVersion": "apps/v1",
    "metadata": {
        "name": "pgeorg-fhir-deployment",
        "namespace": "default",
        "selfLink": "/apis/apps/v1/namespaces/default/deployments/pgeorg-fhir-deployment",
        "uid": "a0ec8464-b108-4e17-bce6-d5587e2bd995",
        "resourceVersion": "21357696",
        "generation": 1,
        ...,
    },
    ...,
}
```
The Fhir ReplicaSet object owned by above object:
```json
{
    "kind": "ReplicaSet",
    "apiVersion": "apps/v1",
    "metadata": {
        "name": "pgeorg-fhir-deployment-57697ff5dc",
        "namespace": "default",
        "selfLink": "/apis/apps/v1/namespaces/default/replicasets/pgeorg-fhir-deployment-57697ff5dc",
        "uid": "93b644ad-5987-4485-93a5-24566fcc09c6",
        ...,
        "labels": {
            "aadpodidbinding": "pgeorg-fhir-identity",
            "deployment": "pgeorg-fhir-deployment",
            "pod-template-hash": "57697ff5dc"
        },
        ...,
        "ownerReferences": [
            {
                "apiVersion": "apps/v1",
                "kind": "Deployment",
                "name": "pgeorg-fhir-deployment",
                "uid": "a0ec8464-b108-4e17-bce6-d5587e2bd995",
                ...,
            }
        ]
    },
    ...,
}
```

The running Fhir Pod object owned by above object:
```json
{
    "kind": "Pod",
    "apiVersion": "v1",
    "metadata": {
        "name": "pgeorg-fhir-deployment-57697ff5dc-xr8pr",
        "generateName": "pgeorg-fhir-deployment-57697ff5dc-",
        "namespace": "default",
        "selfLink": "/api/v1/namespaces/default/pods/pgeorg-fhir-deployment-57697ff5dc-xr8pr",
        "uid": "5d7bc936-58bd-4872-a51f-68bc0f240e26",
        ...,
        "labels": {
            "aadpodidbinding": "pgeorg-fhir-identity",
            "deployment": "pgeorg-fhir-deployment",
            "pod-template-hash": "57697ff5dc"
        },
        "ownerReferences": [
            {
                "apiVersion": "apps/v1",
                "kind": "ReplicaSet",
                "name": "pgeorg-fhir-deployment-57697ff5dc",
                "uid": "93b644ad-5987-4485-93a5-24566fcc09c6",
                ...,
            }
        ]
    },
    ...,
}
```

## Next phase
Resource Provision module will include the libraries for provisioning and de-provisioning the resources for the Fhir service instances.

### Source Structure
```
workspace-platform/fhir/provision

.
├── Microsoft.Health.Cloud.Fhir.Provision
│   ├── Configuration    // Fhir Server Configuration
│   └── Models           // POCOs for modeling Fhir Server Properties and Configuration  
│      
├── Microsoft.Health.Cloud.Fhir.Provision.Azure  
│   ├── Templates       // Templates for Fhir Service
|   └── Providers       // Resource Providers
|
├── Microsoft.Health.Cloud.Fhir.Provision.K8
|
└── Microsoft.Health.Cloud.Fhir.Provision.Console //  Console App 
```

# Resource Deprovision
Deprovision of a Fhir service instance means that we should delete(hard-delete) all the resources provisioned for that service instance and purge all data in other resources related to that instance.