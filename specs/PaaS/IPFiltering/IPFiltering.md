**Proposal: Virtual Network based IP Filtering support for the Azure API for FHIR**

# Description

As a customer, I want to be able to setup the Azure API for FHIR service such that it should receive traffic from only a specified range of IP addresses and reject anything else.

**Investigation Notes**

Following are the available Virtual Network(Vnet) based solutions:

# Use Virtual Network service endpoints with Azure API for FHIR

It enables Vnet support to multi-tenant Azure services.

Pros:

- Improved security limiting service resources to customers’ VNets only.
- Ability to keep Vnet-Azure service traffic within Azure network backbone, despite forcedtunneling the Internet traffic to on-premises and/or Network virtual appliances (NVAs).

Cons:

- FHIR service resources will be discoverable and reachable via public DNS entries/endpoints.
- Cannot restrict access to only customer’s resources from their virtual networks. It locks down access to the entire service, instead of specific resources.
- It does not provide an approval workflow that is required to orchestrate connectivity and security to the FHIR resources.
- On-premises firewalls have to be open to the public Internet to reach the FHIR service.
- On-premises traffic to the FHIR service has to go through ExpressRoute public peering or the public Internet.

To understand more about Virtual Network service endpoints, [What is Virtual Network service endpoints for Azure resources?](https://docs.microsoft.com/en-us/azure/virtual-network/virtual-network-service-endpoints-overview) is a great place to start.

# Use Private Link with Azure API for FHIR

Private Link enables service providers to provide access to customers securely in their virtual network while maintaining the ability to manage and support the resources in the service providers subscription.
It is a new functionality for selected Azure PaaS services that allows customers to create a private endpoint in their virtual network.


Pros: 

- This provides us with a way to grant customers secure access to the FHIR service, with traffic never traversing the internet, while retaining the ability to manage the service and underlying infrastructure.
- A private IP to reference the FHIR service resources.
- Traffic to the FHIR service traverses the Microsoft network.
- Users access is now restricted solely to the specific FHIR resource not the whole service, so customer will have complete control of data egress.
- Private endpoints can be created to resources in different regions to the virtual network and even different tenants.
- It provides an approval workflow that is required to orchestrate connectivity and security to the FHIR resources.
- Cleaner firewall configuration with a resource mapping to the network and workflow for separation of duties.
- Private Link offers a unique connectivity option for customers consuming services on Azure, the premium
of using Private Link will be charge to **customer subscription** with the following components:
1) Per Private Endpoint resource: 1 cent per hour will be charge based on the number of hours using Private Endpoint resources in their subscription.
2) Per data processed: 1 cent per GB will be charged for ingress and egress for data processed by a given deployment using private endpoints.

Cons:

- It is only just in preview. It will go GA next month.
- It cannot co-exist with service endpoints in the same subnet at the moment.
- It doesn't abide by NSG rules. Customer can have NSG's on a subnet with Private Links, and other traffic will still abide by them, but Private Link traffic will not.
- We must use a Standard Load Balancer to our front end service; we cannot use a Basic Load Balancer.

To understand more about Private Link, [What is Private Link for Azure resources?](https://docs.microsoft.com/en-us/azure/private-link/) is a great place to start.

**Onboarding Documentation**

[Onborading process](https://microsofthealth.visualstudio.com/Health/_git/health-paas-docs/pullrequest/12241?path=%2Fspecs%2FIPFiltering%2FIPFiltering.md&_a=overview)    
[Onboarding overview](https://nam06.safelinks.protection.outlook.com/?url=https%3A%2F%2Fmicrosoft.sharepoint.com%2Fteams%2FWAG%2FAzureNetworking%2F_layouts%2F15%2FDoc.aspx%3Fsourcedoc%3D%257B702DF80F-B839-4E26-B542-CD32FEBBA388%257D%26file%3DPrivate%2520Link%2520for%2520PaaS%2520Onboarding%2520Overview%2520.pptx%26action%3Dedit%26mobileredirect%3Dtrue&data=02%7C01%7CAnkita.Rudrawar%40microsoft.com%7Cb1235bfadb9641f4c6d308d75721a63f%7C72f988bf86f141af91ab2d7cd011db47%7C1%7C0%7C637073674343364595&sdata=AzIl%2BVJa3sQjxqtBmJ876Dr9LIDVvgy3XqKd3EWFpK4%3D&reserved=0)  
[Private Endpoints PM Spec](https://nam06.safelinks.protection.outlook.com/ap/w-59584e83/?url=https%3A%2F%2Fmicrosoft.sharepoint.com%2F%3Aw%3A%2Fr%2Fteams%2FWAG%2FAzureNetworking%2F_layouts%2F15%2FDoc.aspx%3Fsourcedoc%3D%257BC91D7F66-C00C-4160-AED0-A671C5400FC6%257D%26file%3DPrivate%2520Endpoints%2520PM%2520Spec.docx%26action%3Ddefault%26mobileredirect%3Dtrue%26cid%3Df4e2406a-0244-4619-b206-f42f176d6862&data=02%7C01%7CAnkita.Rudrawar%40microsoft.com%7Cb1235bfadb9641f4c6d308d75721a63f%7C72f988bf86f141af91ab2d7cd011db47%7C1%7C0%7C637073674343364595&sdata=bbjVrD3K5L4qTOzrvMRTfj3qKwbdSb416co4SnkucbI%3D&reserved=0)  
[User Experience](https://nam06.safelinks.protection.outlook.com/ap/p-59584e83/?url=https%3A%2F%2Fmicrosoft.sharepoint.com%2F%3Ap%3A%2Fr%2Fteams%2FWAG%2FAzureNetworking%2F_layouts%2F15%2FDoc.aspx%3Fsourcedoc%3D%257B49BC559B-DD5E-46A5-A288-B1B55C66CC5F%257D%26file%3DPrivate%2520Endpoints%2520UX.pptx%26action%3Dedit%26mobileredirect%3Dtrue&data=02%7C01%7CAnkita.Rudrawar%40microsoft.com%7Cb1235bfadb9641f4c6d308d75721a63f%7C72f988bf86f141af91ab2d7cd011db47%7C1%7C0%7C637073674343374594&sdata=iOLPYttNUZacxeFm76WwWZCjHft13fLWesi2f8b65OY%3D&reserved=0)
