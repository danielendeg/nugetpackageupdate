## Health-PaaS Resource Governance
### Description
Currently the Resolute team hosts several instances of FHIR servers on a Service Fabric node without regard to
computing resources being consumed such as CPU and Memory. This allows for the possibility of the "noisy neighbor" problem
where a single instance of a FHIR server that is particularly active can consume all the resources of the node and starve the
other FHIR instances on the same node. To prevent this problem from occuring we must be able to enforce limitations on how much of the
CPU and Memory a particular FHIR server can demand. Once the PaaS solution is able to control the resources consumed
we will be able to define a series of sizes, refered to as scale units, with increasing amounts of CPU and Memory. Customers will
then be able to pick the appropriate size FHIR server for their use case.

### Glossary
* Scale Unit - A non-linear unit of size for a FHIR Server in relation to allocated CPU and Memory.

### Out of Scope
* The UI in Azure Portal for increasing FHIR server scale unit size.
* The CPU and Memory values for scale units larger than the default size.
* Pricing and billing based on scale units.
* Reporting usage metrics to customers

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
the nodes rebalanced to accomadate. If this fails a rolling upgrade can be triggered manually from Powershell.


The VMs currently used on the Service Fabric nodes are F4's which have 4 CPU cores and 8 GB of Memory. We will start by setting the
ResourceGovernancePolicy values to a default size of **1 CPU core** and **2 GB Memory**.  By monitoring these starting values and listening
to customer feedback we will tweak these values as necessary and use the data to define larger scale units for those who need more resources
than the default.

#### Increasing Scale Units
Once we are able to enforce resource limitations we will then define a series of larger scale units and allow customers to select the scale
unit that is appropriate for them through ARM. The existing ARM flow is detailed in the [Project Resolute Design Doc](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fdoc%2Fproject-resolute.md&version=GBmaster&createIfNew=true&anchor=arm-resource-provider-service).
The updates to this process are as follows...

1. ARM accepts create/update requests with a ServiceDescription. This ServiceDescription will be updated to have a **ScaleUnitsConfiguration**
similar to the existing **CosmosDBConfiguration**.
2. ARM writes the **ScaleUnitsConfiguration** to the GlobalDB account entry and provisioning entry.
3. The RP Worker picks up the provisioning entry and maps the **ScaleUnitsConfiguration** to a configured amount of CPU Cores and MB of Memory.
4. These CPU and Memory values are passed to Service Fabric as parameters defined in the ApplicationManifest.xml described above.
5. Service Fabric will provision a new FHIRServicePkg ServiceType with the given CPU and Memory requirements.

#### Azure Portal
After ARM is updated to handle requests for different sized FHIR Servers the Azure Portal will be updated with a UI component to describe the
FHIR Server sizing and allow customers to select the one that is best for their use case. However the look and feel of this component will be
determined at a later date.

### Metrics
Performance counters for the Service Fabric and it's nodes are already collected and published to Geneva. We can continue using this existing
telemetry.

A seperate feature is planned to publish usage metrics to the customer for their FHIR server. This will include metrics for CPU and Memory usage
so that customers can be aware of how many resources they are using and if they are approaching the limits of their scale unit.

### Testing
A new test cluster with the ResourceGovernancePolicies described above will be spun-up. Then the load tests will be executed against this
test cluster to evaluate performance given the proposed resource limitations. 

We will also test updating an existing test cluster with ResourceGovernancePolicies to make sure that the new policies are picked up and
enforced. This can be evaluated from the Service Fabric Management Portal.