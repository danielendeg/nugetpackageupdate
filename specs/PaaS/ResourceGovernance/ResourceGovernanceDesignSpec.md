## Health-PaaS Resource Governance
### Description
Currently the Resolute team hosts several instances of FHIR servers on a Service Fabric node without regard to
computing resources being consumed such as CPU and Memory. This allows for the possibility of the "noisy neighbor" problem
where a single instance of a FHIR server that is particularly active can consume all the resources of the node and starve the
other FHIR instances on the same node. To prevent this problem from occurring we must be able to enforce limitations on how much of the
CPU and Memory a particular FHIR server can demand. Once the PaaS solution is able to control the resources consumed
we will be able to define a series of sizes, referred to as scale units, with increasing amounts of CPU and Memory. Customers will
then be able to pick the appropriate size FHIR server for their use case.

### Glossary
* Scale Unit - A non-linear unit of size for a FHIR server in relation to allocated CPU and Memory.

### Out of Scope
* The UI in Azure Portal for increasing FHIR server scale unit size.
* The CPU and Memory values for scale units larger than the default size.
* Pricing and billing based on scale units.
* Reporting usage metrics to customers

### Pre-requisites
* Must have horizontal auto-scaling enabled for backend nodes.

### Solution
#### Enforcing Resource Limitations
We will start by limiting the resources of FHIR server instances to a default using Service Fabric's [ResourceGovernancePolicy](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-resource-governance)
settings in the FHIRApplication ApplicationManifest.xml.
```XML
  <ServiceManifestImport>
    <ServiceManifestRef ServiceManifestName='ServicePackageA' ServiceManifestVersion='v1'/>
    <Policies>
      <ServicePackageResourceGovernancePolicy CpuCores="[CpuCores]"/>
      <ResourceGovernancePolicy CodePackageRef="CodeA1" CpuShares="[CpuSharesA]" MemoryInMB="[MemoryA]" /> />
    </Policies>
  </ServiceManifestImport>
```
With these settings you can define how many CPU cores and how many MB of memory an instance of each ServiceType can have on a node.
When an instance of a particular ServiceType is instantiated, FHIRServicePkg in our case, Service Fabric spins up the service on a
node with the requested amount of resources available and reserves the requested resources for that service. Adding these changes to 
the ApplicationManifest.xml will cause a version change of the manifest and trigger a rolling upgrade of services across the Service
Fabric. This will cause all FHIR services currently running in the Service Fabric to be upgraded to the new resource limitations and
the nodes rebalanced to accommadate. If this fails a rolling upgrade can be triggered manually from Powershell.

The VMs currently used on the Service Fabric nodes are F4's which have 4 CPU cores and 8 GB of Memory with an average of 6-7 FHIR servers
per node.

**Current Usage at Backend Node Level**

| Region    | Avg CPU Usage | Avg Max CPU Usage | Avg Memory Usage | Avg Max Memory Usage |
|-----------|---------------|-------------------|------------------|----------------------|
| US West 2 | 10%           | 20%               | 5.5 GB           | 7.5 GB               |
| UK West   | 2%            | 10%               | 6.0 GB           | 7.0 GB               |
| NC US     | 2%            | 20%               | 6.5 GB           | 7.5 GB               |

On average a single FHIR server will use ~8% of a single core and ~1 GB Memory. During peaks of
activity a FHIR server will use ~16% of a single core and ~1.25 GB Memory.

We will start by setting the ResourceGovernancePolicy values to a default size of **2 GB Memory**. To start we will not limit CPU because
of the very little use of CPU by a single FHIR server. This is a configuration change that can be changed in the future. By monitoring 
these starting values and listening to customer feedback we will tweak these values as necessary and use the data to define larger scale
units for those who need more resources than the default.

#### Increasing Scale Units
Once we are able to enforce resource limitations we will then define a series of larger scale units and allow customers to select the scale
unit that is appropriate for them through ARM. The existing ARM flow is detailed in the [Project Resolute Design Doc](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fdoc%2Fproject-resolute.md&version=GBmaster&createIfNew=true&anchor=arm-resource-provider-service).
The updates to this process are as follows...

1. ARM accepts create/update requests with a ServiceDescription. This ServiceDescription will be updated to have a **ScaleUnitsConfiguration**
similar to the existing **CosmosDBConfiguration**.
2. ARM writes the **ScaleUnitsConfiguration** to the GlobalDB account entry and provisioning entry.
3. The RP Worker picks up the provisioning entry and maps the **ScaleUnitsConfiguration** to a configured amount of CPU cores and MB of Memory.
4. These CPU and Memory values are passed to Service Fabric as parameters defined in the ApplicationManifest.xml described above.
5. Service Fabric will provision a new FHIRServicePkg ServiceType with the given CPU and Memory requirements.

#### Azure Portal
After ARM is updated to handle requests for different sized FHIR Servers the Azure Portal will be updated with a UI component to describe the
FHIR Server sizing and allow customers to select the one that is best for their use case. However the look and feel of this component will be
determined at a later date.

#### Cost
Today the cost of the VM's that make up the Service Fabric is paid for by the Resolute team. There is work planned to change the billing process
so that customers are billed for their VM usage but until that is done any changes to the amount of VM's will be paid for by the Resolute team.
This is important because the number of VM's will go up due to Resource Governance changes. 

Currently we use 50 F4 VMs for hosting FHIR Servers which cost $0.199/hour for Pay-As-You-Go [pricing](https://azure.microsoft.com/en-us/pricing/details/virtual-machines/linux/#Windows)
costing the team today **~$7,100/month**. And as of 07/08/2019 we have 680 FHIR servers (340 accounts each with 2 instances) running
in production. We will also assume all customers are set to Scale Unit 1 to start as it does not make sense to allow the
customers to upgrade their Scale Unit before customer billing is enabled. That means 3 FHIR servers on a single node (not 4
to save some computational resources for background VM processes). That ends up being \~227 VM's at a cost of **~$32,500/month**.

There is some opportunity for cost savings by switching to a different VM series that better supports the low CPU usage and high Memory
usage of a FHIR server. Looking at the Av2 series they offer the combination of high amounts of Memory with a lower core count CPU
for a cheaper price than the F series. There are also some opportunities for increasing the size of the VM's. For example instead of just 
going from an F4 to an A4v2 we can increase the size of the VM to one with more CPU cores and more Memory to fit more FHIR Servers onto a
single VM. For example a A8mV2 with 8 cores and 64 GB Memory can potentially support up to 31 FHIR servers depending on how much CPU and
Memory should be reserved for background/OS operations. This would reduce the number of VM's need from 227 to 22. And at a cost of
$0.662/hour that totals to **~$10,500/month** or a savings of **~$21,500/month** from the original estimate. 

However regardless of how much savings can be made by adjusting the VM's to a better form factor and size, there is still a non-negligible 
operating cost added until the billing work is also ready for release. The only way to avoid this completely is to make the billing changes a 
pre-requisite for the Resource Governance work. While some portions of the Resource Governance changes can be implemented in parallel, the feature
would not be complete and enabled until the billing changes are also ready to be released. This could delay the release of Resource Governance
and Scale Units features as it already dependent on auto-scaling and would then be dependent on a second feature. 

### Metrics
Performance counters for the Service Fabric and it's nodes are already collected and published to Geneva. We can continue using this existing
telemetry.

A separate feature is planned to publish usage metrics to the customer for their FHIR server. This will include metrics for CPU and Memory usage
so that customers can be aware of how many resources they are using and if they are approaching the limits of their scale unit.

### Testing
A new test cluster with the ResourceGovernancePolicies described above will be spun-up. Then the load tests will be executed against this
test cluster to evaluate performance given the proposed resource limitations. 

We will also test updating an existing test cluster with ResourceGovernancePolicies to make sure that the new policies are picked up and
enforced. This can be evaluated from the Service Fabric Management Portal.