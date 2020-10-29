# Business Justification
To support security requirements Az SecPack must be installed on all infrastructure deployed to production. This set of tools performs security vunerability monitoring and reports details back to Geneva. This document provides an overview of how Az SecPack will be configured and deployed inside of the AKS Workspace infrastructure.

# Scenarios
The following scenarios will be enabled:

- Vunerability Scanning -- Configuration and installation of an approved suite of tools to support vunerability monitoring within our AKS infrastructue
- Compliance -- Meet the S360 compliance requirement of running Az SecPack
- Log Collection -- Aggregation of security related logs (including IFxAudit Logs) within the Geneva Platform
- No Code Deployment -- Adhere to 'No Code' means for deployment to reduce need for custom scripts.

# Design 

## Prerequisites

1. It is assumed that a Azure Keyvault will be deployed to support the Workspace AKS infrastructure. This will be used to store certificates needed by Geneva. This would be a global keyvault as the certificates would be used across all regions.
1. Its assumed that we will have separate, developer Azure Container Registry (ACR) which get updated via build pipelines. The Workspace ACR will be synced with these developer ACRs during Ev2 deployment, thus ensuring that needed images are present. The work needed for this syncing is outside the scope of this document.

## Geneva
Geneva will host all logs collected by the Mdsd agents running on the AKS cluster. Within our dev (ResoluteNonProd) and prod (ResoluteProd) accounts a single, new namespace will be created called 'Workspace'. Mdsd agents will be configured to store all data within this namespace. Since mdsd runs at the Kubernetes Node level all logs will flow into a single namespace.

### Configuration

#### Accounts
Geneva Accounts (ResoluteNonProd and ResoluteProd) will be manually created and maintained within the Genva UI Portal.

#### Namespace
The __Workspace__ namepace will be manually created and maintained within the Genva UI Portal.

#### Authentication
Geneva uses certificates for authenticating agents who attempt to submit data to a namespace. We will make use of Geneva's [Azure Keyvault Certificate](https://genevamondocs.azurewebsites.net/collect/authentication/keyvault.html) support to not only support authentication but also handle certificate rotation. This will be accomplished as follows:

1. Create a certificate within the global Azure Keyvault. This certificate will be created as specified [here](https://genevamondocs.azurewebsites.net/collect/authentication/keyvault.html#how-to-create-an-azure-keyvault-certificate). 
    1. The certificate provider will be OneCert, as this is the only provider trusted by Geneva.
    1. The subject will have a domain which we have previously registered with OneCert: __*internal.mshapis.com__ for dev and __*internal.azurehealthcareapis.com__ for prod.
    1. The Subject Alternative Name (SAN) will contain the regex '*.geneva.keyvault.*' and end in one of our registered domains.
1. Register the SAN with Geneva and associate it with our Namespace. This will allow mdsd to reach Geneva when using the certificate.

The creation of the certificate as well as the registration in Geneva will be done via Ev2.

## AzSecPack
To meet security requirements, AzSecPack must be running on the AKS infrastructure. To ease deployment the AzSecPack team maintains a [OneShot Container](https://dev.azure.com/msazure/One/_git/Compute-Runtime-Tux-GenevaContainers?_a=preview&path=%2FDockerRunDocumentation_azsecpack_install.md&version=GBmaster), which configures and runs the following:

- The Az Sec Pack vunerability scanners
- The Linux Monitoring Agent (Mdsd).

The OneShot Container deploys as a Daemon Set, ensuring that a copy runs on each Node within the AKS cluster. All logs collected will be reported back to the Namespaces described in [here](#Geneva)

## Customization of the Oneshot Container
The following section details certain modifications that will be needed to the existing Geneva hosted OneShot container.

### Acquiring Geneva Certificate
The OneShot Container will be updated to use the [Secrets Store CSI Driver](https://github.com/Azure/secrets-store-csi-driver-provider-azure) to manage certificates from Azure KeyVault. This includes the initial certificate retrieval as well as retrieving certs when they've been rotated by KeyVault (we will make use of its [secret rotation](https://github.com/kubernetes-sigs/secrets-store-csi-driver/blob/master/docs/README.rotation.md) feature to achieve this). The driver will make use of a Managed Identity with access to the Keyvault when retrieving the certificates.

### Supporting Certificate Rotation
As stated here, the [mdsd](https://genevamondocs.azurewebsites.net/collect/authentication/keyvaultlogsagentconfig.html#configure-linuxma-mdsd-to-use-akv-certificates) supports certificate rotation. It does this by monitoring a folder for new/updated certificate files. However, it needs to be provided certain configuration values to enable this feature. The current OneShot Contain does not provide a way to pass these configuration values to the mdsd agent. We will need to modify it to support the following:

1. Allow the following parameters to be passed to the mdsd configuration file:
    1. MONITORING_GCS_AUTH_ID_TYPE
    1. MONITORING_GCS_AUTH_ID
    1. MDSD_AKV_CERTIFICATE_STORE_PATH
1. Allow the setup script to proceed if a specific CERT and KEYFILE are not provided.

The above changes may also be contributed back to Geneva. This would be ideal as they could maintin future updates.

### Hosting of the OneShot Container
We will host the modified OneShot Container inside of our own developer Azure Container Registry (ACR). The reason is two-fold:

1. We will need to customize the vanilla OneShot container and will need to control deploying and hosting it.
1. Genva doesn't host their images inside of National Clouds.

There is an ongoing effort to sync these development ACRs into the Workspace ACR as part of the Ev2 deployment. So it is assumed that our custom image will be available and up to date when we need to install Az SecPack.

## Ev2
In order to support declaritive deployment, the installation of Az SecPack will be incorporated into the existing Ev2 deployment currently being developed for the AKS Workspace. Az SecPack will be included as a Helm Chart and invoked using Ev2's Helm Chart extension.

### Genva Certificate
Ev2\ARM will be configured to create a certificate for Geneva access inside of the global Keyvault. It will then be [registered](https://genevamondocs.azurewebsites.net/collect/authentication/keyvault.html#how-to-create-an-azure-keyvault-certificate) against the Workspace namespace to give the mdsd agent access. This will be done using [Ev2's Geneva extenstions](https://ev2docs.azure.net/features/extensibility/http/common/Microsoft.Geneva.Logs.html?q=geneva%20name).

### Managed Identity
Ev2\ARM will be configured to create a Managed Identity which read access to the Global Keyvault. This will be used by the [Secrets Store CSI Driver](https://github.com/Azure/secrets-store-csi-driver-provider-azure) to retrieve the Geneva certificate.

- This can be same managed identity already associated to the AKS Cluster.

## Open Issues

- Ev2's Helm Chart extension is only available in public clouds. They plan to role this out to Fairfax in the next few weeks, and into the remaining clouds in the coming months. It's desirable to use the Helm extension versus a custom script but we do need to be aware of their deployment roadmap.
- Determine if the OneShot Container supports Geneva Metrics in addition to logs. If so, how do we allow pods to communicate to the metrics/fluentd agent running inside of the Az SecPack Pod?
