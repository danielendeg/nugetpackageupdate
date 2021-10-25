# Jupiter Autoscale
The purpose of this document is describe how the autoscaling (replica instances) of service fabric is implemented for Fhir Service in Jupiter.
There are three major factors (triggers) that determine how services are scaled
* **Lower load threshold:** determines when services to be scaled in. If the average load (in our case CPU usage) of all instances is lower than this value, the service fabric will scale-in the service. This value is read from the property <i><b>fhirService_CpuLowerThresholdForScaleIn</b></i> in the account JSON ( as shown below in *Snippet #1*) or from the key value pair from the subscription JSON ( as shown below in *Snippet #2*) in the Global DB. The subscription overrides supercede the resource overrides. Otherwise it defaults to 0.2.
* **Upper load threshold:** determines when the service should scale-out. If the average load (CPU Usage ) of all instances of the partitions (in our case single partition) is greater than this value, then the service fabric will scale out the service. This value is read from the property <i><b>fhirService_CpuUpperThresholdForScaleOut</b></i> in the account JSON ( as shown below in *Snippet #1*) or from the key value pair from the subscription JSON ( as shown below in *Snippet #2*) in the Global DB. The subscription overrides supercede the resource overrides. Otherwise, it defaults to 0.7.
* **Scaling interval**: determines how often the trigger should be checked.For Jupiter, this value defaults to 1 minutes.

```
{
	"resource": {
		"systemOverrides": {
			"overrides": {
				"[key]": "[value]"
			}
		}
	}
}
``` 
  *Snippet #1: Account Json snippet from Global DB*   
```

{
	"subscriptionId": "someId",
	"systemOverrides": {
		"fhirServiceOverrides": {
			"overrides": {
				"[key]": "[value]"
			}
		}
	}
}
 ```
  *Snippet #2: Subscription Json snippet from Global DB*   

  The above described scaling trigger is used PartitionInstanceCountScaleMechanism. This mechanism is applied using the following properties

  * **Scale Increment**: How many instances should be added or removed dduring scaling-in or scaling-out. Jupiter gets this value from the property <i><b>fhirService_InstanceScaleIncrement</b></i> in the account JSON ( as shown above in *Snippet #1*) or from the key value pair from the subscription JSON ( as shown above in *Snippet #2*) in the Global DB. The subscription overrides supercede the resource overrides. Otherwise , it defaults to 3.
  * **Maximum Instance Count**: defines the upper limit for scaling. This value is set to -1; the services will scale out as much as possible to the maximum of the number of nodes available in the cluster
  * **Minimum Instance Count**: defines the lower limit for scaling. If the number of instance counts reaches this value, then services will not scale in regardless of the load. This value is read from the property <i><b>FhirService_InstanceCount</b></i> in the account JSON ( as shown above in *Snippet #1*) or from the key value pair from the subscription JSON ( as shown above in *Snippet #2*) in the Global DB. The subscription overrides supercede the resource overrides. Otherwise, it is set to 1 in single node cluster or 2 for any other.

Based on the above described triggers and mechanism for scaling,  a service scaling policy is set in the service fabric using the C# API in ```WorkspaceFhirServiceFabricProvisioningProvider``` class in Jupiter