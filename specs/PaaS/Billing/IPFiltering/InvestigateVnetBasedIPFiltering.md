**Proposal: How would IP Filtering work with VNet for Azure API for FHIR**

-	Currrently, we create a vnet and multiple subnets (frontend, backend, management) as part of the cluster json arm template deployment.
-	Our cluster vms are part of this vnet.
-	We also create multiple Network Security Groups similarly along with security rules as part of cluster deployment. One rule for example allows rdp onto the virtual machines.
-	On customer request, we would create a NSG rule limiting access to the front end subnet. The request itself will be saved as part of the account document so that if we need to recreate the account for some reason, we can add back the rule. 
-	I am thinking that this will not be part of cluster arm template and will instead be a script. We don’t want to redeploy cluster for every customer request.
-	Pros
1. Azure manages the ip filtering logic if we use nsg rule.
1. Easy to add/remove the nsg rule.
-	Cons
1. We will create the rule on the subnet range for front end service. Requests for all customers for the given range will be dropped.
1. Say there is a scenarios such as Customer X having two fhir accounts, one for dept A and another for dept B. If customer X wants dept A fhir to filter out a certain ip range and dept B fhir to allow, the nsg rule filter will not work as this will filter out all requests in the range.
1. We might start hitting Azure limits if we have a large number of customer requests since we have a single subscription in which we create our resources.  See limits.

**Investigation Notes**

**What is Azure Virtual Network?**

Azure Virtual Network (VNet) is the fundamental building block for your private network in Azure. VNet enables many types of Azure resources, such as Azure Virtual Machines (VM), to securely communicate with each other, the internet, and on-premises networks. VNet is similar to a traditional network that you'd operate in your own data center, but brings with it additional benefits of Azure's infrastructure such as scale, availability, and isolation.

**VNet concepts**
- **Address space**: When creating a VNet, you must specify a custom private IP address space using public and private (RFC 1918) addresses. Azure assigns resources in a virtual network a private IP address from the address space that you assign. For example, if you deploy a VM in a VNet with address space, 10.0.0.0/16, the VM will be assigned a private IP like 10.0.0.4.
- **Subnets**: Subnets enable you to segment the virtual network into one or more sub-networks and allocate a portion of the virtual network's address space to each subnet. You can then deploy Azure resources in a specific subnet. Just like in a traditional network, subnets allow you to segment your VNet address space into segments that are appropriate for the organization's internal network. This also improves address allocation efficiency. You can secure resources within subnets using Network Security Groups. For more information, see Security groups.
- **Regions**: VNet is scoped to a single region/location; however, multiple virtual networks from different regions can be connected together using Virtual Network Peering.
- **Subscription**: VNet is scoped to a subscription. You can implement multiple virtual networks within each Azure subscriptionand Azure region.

[More Information on VNet](https://docs.microsoft.com/en-us/azure/virtual-network/virtual-networks-overview)

**Security groups**

You can filter network traffic to and from Azure resources in an [Azure virtual network](https://docs.microsoft.com/en-us/azure/virtual-network/virtual-networks-overview) with a network security group. A network security group contains security rules that allow or deny inbound network traffic to, or outbound network traffic from, several types of Azure resources. To learn about which Azure resources can be deployed into a virtual network and have network security groups associated to them, see [Virtual network integration for Azure services](https://docs.microsoft.com/en-us/azure/virtual-network/virtual-network-for-azure-services). For each rule, you can specify source and destination, port, and protocol.

**Security rules**

A network security group contains zero, or as many rules as desired, within Azure subscription [limits](https://docs.microsoft.com/en-us/azure/azure-subscription-service-limits?toc=%2fazure%2fvirtual-network%2ftoc.json#azure-resource-manager-virtual-networking-limits).

**Augmented security rules**

Augmented security rules simplify security definition for virtual networks, allowing you to define larger and complex network security policies, with fewer rules. You can combine multiple ports and multiple explicit IP addresses and ranges into a single, easily understood security rule. Use augmented rules in the source, destination, and port fields of a rule. To simplify maintenance of your security rule definition, combine augmented security rules with [service tags](https://docs.microsoft.com/en-us/azure/virtual-network/security-overview#service-tags) or [application security groups](https://docs.microsoft.com/en-us/azure/virtual-network/security-overview#application-security-groups). There are limits to the number of addresses, ranges, and ports that you can specify in a rule. For details, see [Azure limits](https://docs.microsoft.com/en-us/azure/azure-subscription-service-limits?toc=%2fazure%2fvirtual-network%2ftoc.json#azure-resource-manager-virtual-networking-limits).

[More Information on NSGs](https://docs.microsoft.com/en-us/azure/virtual-network/security-overview#network-security-groups)
