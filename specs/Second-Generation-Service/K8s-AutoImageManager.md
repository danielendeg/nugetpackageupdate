# AutoImageManager

The purpose of this controller is to stamp CRDs with a container image and track overall progress across the Kubernetes cluster. This could be for upgrade or initial deployment.

**Note** I like to talk about concrete examples. In many places I talk about DICOM crd, & dicom controller. You can substitute dicom for your specific CRD.

## Goals

1. Orchestrate upgrade across a Single Cluster
1. Easy to determine logic of 
    1. Success
    1. InProgress
1. Simple logic
1. Stamp CRDs with their initial container image

### Potential Future Goals

1. Percentage canary roll outs
    1. ex: Rollout to 1% of customers first
1. [flagger](https://github.com/weaveworks/flagger) style integration with a service mesh
1. Advanced heuristics to determine if updated service is successfully operating
    1. not just health check endpoint.
1. Determining failure & reason for failure
    1. currently handled by deployment system of failing to reach updated state within time
    1. could add fast fail & timeouts
    1. common failures to enable quick diagnosis of
        1. 100% Image pull failure
        1. 100% container not going healthy
        1. partial Image pull failure
        1. partial container not going healthy

### Non-Goals

1. Determining the cross cluster orchestration story
1. Determining the status of a previous upgrade request
1. Ensuring that a previous upgrade drives to completion before starting another one
1. Control upgrade of the dicom controller
    1. Resource Worker would use kubectl rollout

## Feedback

* Do not allow more then one operation at a time.
  * Upgrade, or setting parameters on the object should be serialized
    * Potential future goal - Have an operation object on the CRD
      * have smart clients that can check & fail
      * other option of have an admission controller fail if previous operation object is not in progress
        * this is less desirable as it makes fixing things harder
* should we check for failure before upgrade?
  * Not Necessary
* more documentation about process of upgrading infrastructure vs service deployments
  * **TODO** Put links to ev2 deployment docs (once they are written)


## Architecture

To support this, we will create a new Kubernetes custom resource known as `ServiceImage` which holds the image name for CRs to use. The schema for this object is:

**TODO** move these objects to code and create link to them

```go
// structs to support ServiceImage

// ServiceImage is the state to drive all images towards
type ServiceImage struct {
    metav1.TypeMeta   `json:",inline"`
    metav1.ObjectMeta `json:"metadata,omitempty"`

    Spec   ServiceImageSpec   `json:"spec,omitempty"`
    Status ServiceImageStatus `json:"status,omitempty"`
}

// ServiceImageSpec is the list of specs to drive towards
type ServiceImageSpec struct {
    DefaultImage       string `json:"defaultImage"`                 // the container image with tag or digest
    tiers []ServiceImageTierSpec `json:"tiers"`
}

// ServiceImageTierSpec defines the spec for an upgrade tier
type ServiceImageTierSpec struct {
    UpgradeTier string `json:"upgradeTier,omitempty"` // match label "upgrade-tier" to determine which services to upgrade
                                                      // use empty for matching to all not specified

    Image       string `json:"image,omitempty"`       // the container image with tag or digest, if empty uses DefaultImage
    Priority    int    `json:"priority,omitempty"`    // higher number goes first in upgrade order
}

// ServiceImageStatus the status of the ServiceImage
type ServiceImageStatus struct {
  ObservedGeneration int64                       `json:"observedGeneration"`  // the generation that this status represents
  Conditions         []ServiceImageConditionType `json:"conditions"`          // conditions of the status
  TierStatus         []ServiceImageTierStatus    `json:"tierStatus"`          // conditions of each tier
  CurrentPriority    int                         `json:"priority"`            // current upgrade priority that is being processed
}

// ServiceImageTierStatus defines the observed state of each tier upgrade
type ServiceImageTierStatus struct {
  UpgradeTier        string             `json:"upgradeTier"`          // match label "upgrade-tier" to determine which services to upgrade
  NewDeploymentImage string             `json:"newDeploymentImage"`   // image that will be used if a new deployment is created
  Conditions         []metav1.Condition `json:"conditions"`           // conditions of the status
}

// These are valid conditions of a ServiceImage.
const (
  // The current requested image deployment has been completed
  // This is ignoring new provisioned CRs
  ServiceImageComplete string = "Complete"
  
  // In progress means that this update has been started
  // for an UpdateTier if it is not been started it will be false
  ServiceImageInProgress string = "InProgress"
)
```

```go
// The CRD under management needs to have a few requirements
// the spec needs "ImageSpec" embedded in it
// the status needs "Conditions" ImageSpecUpToDate & ImageSpecDeployed

// ImageSpec contains the fields a CRD needs to implement to be controlled via the AutoImageManager
type ImageSpec struct {
	Image string `json:"image"`

	// if AutoImageManager cannot upgrade successfully due to this instance failing to upgrade
	// do we consider it a failure
	FailureHaltsUpdate bool `json:"failureHaltsUpdate,omitempty"`

	// AutoImageManager should apply the image to this instance
	ManuallySpecifiedImage bool `json:"manuallySpecifiedImage,omitempty"`
}

// DicomSpec defines the desired state of Dicom
type DicomSpec struct {
	ImageSpec                `json:",inline"`
	AadPodIdentityResourceID string `json:"aadPodIdentityResourceId"`
	AadPodIdentityClientID   string `json:"aadPodIdentityClientId"`
	KeyVaultURI              string `json:"keyVaultUri"`
}

// DicomStatus defines the observed state of Dicom
type DicomStatus struct {
	ObservedGeneration int64              `json:"observedGeneration"`   // the generation that this status represents
	Conditions         []metav1.Condition `json:"conditions,omitempty"` // conditions of the status
}

// Conditions that status in CRD needs to implement
const (
	// ImageSpecUpToDate the CRD is being used by all pods in Deployment
	ImageSpecUpToDate string = "UpToDate"
	// ImageSpecDeployed the deployment has gone Available at least once in the past
	ImageSpecDeployed string = "Deployed"
)
```

Additionally, this actual instances will look like:
 
```yaml
apiVersion: services.azurehealthcareapis.com/v1alpha1
kind: ServiceImage
metadata:
  name: dicom-serviceimage-prod
  labels:
    service: dicom
  generation: 2
spec:
  tiers:
  - upgradeTier: ""
    image: ouracr.azurecr.io/dicom-service:v2
    priority: 0
  - upgradeTier: earlyAccess
    image: ouracr.azurecr.io/dicom-service:v3
    priority: 1
status:
  observedGeneration: 2
  conditions:
  - type: Complete
    status: "False"
    lastUpdateTime: "2020-10-12T19:36:36Z"
  - type: InProgress
    status: "True"
    lastUpdateTime: "2020-10-12T19:36:36Z"
  tierStatus: 
  - upgradeTier: ""
    newDeploymentImage: ouracr.azurecr.io/dicom-service:v1
    conditions:
    - type: Complete
      status: "False"
      lastUpdateTime: "2020-10-12T19:36:36Z"
    - type: InProgress
      status: "False"
      lastUpdateTime: "2020-10-12T19:36:36Z"
  - upgradeTier: "earlyAccess"
    newDeploymentImage: ouracr.azurecr.io/dicom-service:v2
    conditions:
    - type: Complete
      status: "False"
      lastUpdateTime: "2020-10-12T19:36:36Z"
    - type: InProgress
      status: "True"
      lastUpdateTime: "2020-10-12T19:36:36Z"
  currentPriority: 1
```

## Flow

The flow for upgrading this is shown in the below sequence document.

**Note** for purposes of this doc we will be modifying DicomUpgradeController and DicomController

[![](https://mermaid.ink/img/eyJjb2RlIjoic2VxdWVuY2VEaWFncmFtXG4gIHBhcnRpY2lwYW50IERlcGxveW1lbnRTeXN0ZW1cbiAgcGFydGljaXBhbnQgQUNSXG4gIHBhcnRpY2lwYW50IEF1dG9JbWFnZU1hbmFnZXJcbiAgcGFydGljaXBhbnQgRGljb21Db250cm9sbGVyXG4gIHBhcnRpY2lwYW50IERpY29tRGVwbG95bWVudFxuICBQYXJ0aWNpcGFudCBDb250cm9sbGVyTWFuYWdlclxuICBEZXBsb3ltZW50U3lzdGVtLT4-QUNSOiBQdXNoIG5ldyBpbWFnZVxuICBEZXBsb3ltZW50U3lzdGVtLT4-QXV0b0ltYWdlTWFuYWdlcjogU2V0IFNlcnZpY2VJbWFnZVxuXG4gIHJlY3QgcmdiYSgxMDUsIDEwNSwgMTA1LCAuMjUpXG4gICAgbm90ZSBvdmVyIEF1dG9JbWFnZU1hbmFnZXI6IFJlY29uY2lsZSBTZXJ2aWNlSW1hZ2UgKGFzeW5jKVxuICAgIEF1dG9JbWFnZU1hbmFnZXItPj5EaWNvbUNvbnRyb2xsZXI6IFVwZGF0ZSBpbWFnZTxici8-YWxsIERpY29tIENSRFxuICAgIERpY29tQ29udHJvbGxlci0-PkRpY29tRGVwbG95bWVudDogVXBkYXRlIFBvZCBTcGVjIGltYWdlXG4gICAgXG4gICAgcmVjdCByZ2JhKDEwNSwgMTA1LCAxMDUsIC4yNSlcbiAgICAgIG5vdGUgb3ZlciBDb250cm9sbGVyTWFuYWdlcjogRG8gcm9sbGluZyB1cGdyYWRlIChhc3luYylcbiAgICAgIENvbnRyb2xsZXJNYW5hZ2VyLT4-RGljb21EZXBsb3ltZW50OiBDcmVhdGUgUmVwbGljYVNldDxicj53L25ldyBJbWFnZVxuICAgICAgQ29udHJvbGxlck1hbmFnZXItPj5EaWNvbURlcGxveW1lbnQ6IENyZWF0ZSBwb2Q8YnI-aW4gbmV3IFJlcGxpY2FTZXRcbiAgICBEaWNvbURlcGxveW1lbnQtPj5BQ1I6IEdldCBuZXcgaW1hZ2VcbiAgICBBQ1ItPj5EaWNvbURlcGxveW1lbnQ6IE5ldyBpbWFnZVxuICAgICAgQ29udHJvbGxlck1hbmFnZXItPj5EaWNvbURlcGxveW1lbnQ6IFZhbGlkYXRlIFBvZCBIZWFsdGh5XG4gICAgICBDb250cm9sbGVyTWFuYWdlci0-PkRpY29tRGVwbG95bWVudDogRGVsZXRlIHBvZDxicj5pbiBvbGQgUmVwbGljYVNldFxuICAgICAgQ29udHJvbGxlck1hbmFnZXItPj5Db250cm9sbGVyTWFuYWdlcjogdW50aWwgb2xkIFJlcGxpY2FTZXQgZW1wdHlcbiAgICAgIERpY29tRGVwbG95bWVudC0-PkRpY29tQ29udHJvbGxlcjogVHJpZ2dlciByZWNvbmNpbGU8YnIvPihVcGRhdGVkIFN0YXR1cylcbiAgICBlbmRcbiAgICBEaWNvbUNvbnRyb2xsZXItPj5BdXRvSW1hZ2VNYW5hZ2VyOiBUcmlnZ2VyIHJlY29uY2lsZTxici8-KFVwZGF0ZWQgU3RhdHVzKVxuICAgIFxuICAgIHJlY3QgcmdiYSgxMDUsIDEwNSwgMTA1LCAuMjUpXG4gICAgICBub3RlIG92ZXIgQXV0b0ltYWdlTWFuYWdlcjogVXBkYXRlIFVwZ3JhZGUgU3RhdHVzXG4gICAgICBBdXRvSW1hZ2VNYW5hZ2VyLT4-RGljb21Db250cm9sbGVyOiBHZXQgU3RhdHVzXG4gICAgQXV0b0ltYWdlTWFuYWdlci0-PkF1dG9JbWFnZU1hbmFnZXI6IFJvbGx1cCBTdGF0dXNcbiAgICBlbmRcblxuICBlbmRcbiAgXG4gIHJlY3QgcmdiYSgxMDUsIDEwNSwgMTA1LCAuMjUpXG4gICAgbm90ZSBvdmVyIEFDUjogVmVyaWZ5IFVwZ3JhZGVcbiAgICBEZXBsb3ltZW50U3lzdGVtLT4-QXV0b0ltYWdlTWFuYWdlcjogUG9sbCBTdGF0dXNcbiAgICBEZXBsb3ltZW50U3lzdGVtLT4-RGVwbG95bWVudFN5c3RlbTogdW50aWwgZG9uZVxuICBlbmQiLCJtZXJtYWlkIjp7InRoZW1lIjoiZGVmYXVsdCIsInRoZW1lVmFyaWFibGVzIjp7ImJhY2tncm91bmQiOiJ3aGl0ZSIsInByaW1hcnlDb2xvciI6IiNFQ0VDRkYiLCJzZWNvbmRhcnlDb2xvciI6IiNmZmZmZGUiLCJ0ZXJ0aWFyeUNvbG9yIjoiaHNsKDgwLCAxMDAlLCA5Ni4yNzQ1MDk4MDM5JSkiLCJwcmltYXJ5Qm9yZGVyQ29sb3IiOiJoc2woMjQwLCA2MCUsIDg2LjI3NDUwOTgwMzklKSIsInNlY29uZGFyeUJvcmRlckNvbG9yIjoiaHNsKDYwLCA2MCUsIDgzLjUyOTQxMTc2NDclKSIsInRlcnRpYXJ5Qm9yZGVyQ29sb3IiOiJoc2woODAsIDYwJSwgODYuMjc0NTA5ODAzOSUpIiwicHJpbWFyeVRleHRDb2xvciI6IiMxMzEzMDAiLCJzZWNvbmRhcnlUZXh0Q29sb3IiOiIjMDAwMDIxIiwidGVydGlhcnlUZXh0Q29sb3IiOiJyZ2IoOS41MDAwMDAwMDAxLCA5LjUwMDAwMDAwMDEsIDkuNTAwMDAwMDAwMSkiLCJsaW5lQ29sb3IiOiIjMzMzMzMzIiwidGV4dENvbG9yIjoiIzMzMyIsIm1haW5Ca2ciOiIjRUNFQ0ZGIiwic2Vjb25kQmtnIjoiI2ZmZmZkZSIsImJvcmRlcjEiOiIjOTM3MERCIiwiYm9yZGVyMiI6IiNhYWFhMzMiLCJhcnJvd2hlYWRDb2xvciI6IiMzMzMzMzMiLCJmb250RmFtaWx5IjoiXCJ0cmVidWNoZXQgbXNcIiwgdmVyZGFuYSwgYXJpYWwiLCJmb250U2l6ZSI6IjE2cHgiLCJsYWJlbEJhY2tncm91bmQiOiIjZThlOGU4Iiwibm9kZUJrZyI6IiNFQ0VDRkYiLCJub2RlQm9yZGVyIjoiIzkzNzBEQiIsImNsdXN0ZXJCa2ciOiIjZmZmZmRlIiwiY2x1c3RlckJvcmRlciI6IiNhYWFhMzMiLCJkZWZhdWx0TGlua0NvbG9yIjoiIzMzMzMzMyIsInRpdGxlQ29sb3IiOiIjMzMzIiwiZWRnZUxhYmVsQmFja2dyb3VuZCI6IiNlOGU4ZTgiLCJhY3RvckJvcmRlciI6ImhzbCgyNTkuNjI2MTY4MjI0MywgNTkuNzc2NTM2MzEyOCUsIDg3LjkwMTk2MDc4NDMlKSIsImFjdG9yQmtnIjoiI0VDRUNGRiIsImFjdG9yVGV4dENvbG9yIjoiYmxhY2siLCJhY3RvckxpbmVDb2xvciI6ImdyZXkiLCJzaWduYWxDb2xvciI6IiMzMzMiLCJzaWduYWxUZXh0Q29sb3IiOiIjMzMzIiwibGFiZWxCb3hCa2dDb2xvciI6IiNFQ0VDRkYiLCJsYWJlbEJveEJvcmRlckNvbG9yIjoiaHNsKDI1OS42MjYxNjgyMjQzLCA1OS43NzY1MzYzMTI4JSwgODcuOTAxOTYwNzg0MyUpIiwibGFiZWxUZXh0Q29sb3IiOiJibGFjayIsImxvb3BUZXh0Q29sb3IiOiJibGFjayIsIm5vdGVCb3JkZXJDb2xvciI6IiNhYWFhMzMiLCJub3RlQmtnQ29sb3IiOiIjZmZmNWFkIiwibm90ZVRleHRDb2xvciI6ImJsYWNrIiwiYWN0aXZhdGlvbkJvcmRlckNvbG9yIjoiIzY2NiIsImFjdGl2YXRpb25Ca2dDb2xvciI6IiNmNGY0ZjQiLCJzZXF1ZW5jZU51bWJlckNvbG9yIjoid2hpdGUiLCJzZWN0aW9uQmtnQ29sb3IiOiJyZ2JhKDEwMiwgMTAyLCAyNTUsIDAuNDkpIiwiYWx0U2VjdGlvbkJrZ0NvbG9yIjoid2hpdGUiLCJzZWN0aW9uQmtnQ29sb3IyIjoiI2ZmZjQwMCIsInRhc2tCb3JkZXJDb2xvciI6IiM1MzRmYmMiLCJ0YXNrQmtnQ29sb3IiOiIjOGE5MGRkIiwidGFza1RleHRMaWdodENvbG9yIjoid2hpdGUiLCJ0YXNrVGV4dENvbG9yIjoid2hpdGUiLCJ0YXNrVGV4dERhcmtDb2xvciI6ImJsYWNrIiwidGFza1RleHRPdXRzaWRlQ29sb3IiOiJibGFjayIsInRhc2tUZXh0Q2xpY2thYmxlQ29sb3IiOiIjMDAzMTYzIiwiYWN0aXZlVGFza0JvcmRlckNvbG9yIjoiIzUzNGZiYyIsImFjdGl2ZVRhc2tCa2dDb2xvciI6IiNiZmM3ZmYiLCJncmlkQ29sb3IiOiJsaWdodGdyZXkiLCJkb25lVGFza0JrZ0NvbG9yIjoibGlnaHRncmV5IiwiZG9uZVRhc2tCb3JkZXJDb2xvciI6ImdyZXkiLCJjcml0Qm9yZGVyQ29sb3IiOiIjZmY4ODg4IiwiY3JpdEJrZ0NvbG9yIjoicmVkIiwidG9kYXlMaW5lQ29sb3IiOiJyZWQiLCJsYWJlbENvbG9yIjoiYmxhY2siLCJlcnJvckJrZ0NvbG9yIjoiIzU1MjIyMiIsImVycm9yVGV4dENvbG9yIjoiIzU1MjIyMiIsImNsYXNzVGV4dCI6IiMxMzEzMDAiLCJmaWxsVHlwZTAiOiIjRUNFQ0ZGIiwiZmlsbFR5cGUxIjoiI2ZmZmZkZSIsImZpbGxUeXBlMiI6ImhzbCgzMDQsIDEwMCUsIDk2LjI3NDUwOTgwMzklKSIsImZpbGxUeXBlMyI6ImhzbCgxMjQsIDEwMCUsIDkzLjUyOTQxMTc2NDclKSIsImZpbGxUeXBlNCI6ImhzbCgxNzYsIDEwMCUsIDk2LjI3NDUwOTgwMzklKSIsImZpbGxUeXBlNSI6ImhzbCgtNCwgMTAwJSwgOTMuNTI5NDExNzY0NyUpIiwiZmlsbFR5cGU2IjoiaHNsKDgsIDEwMCUsIDk2LjI3NDUwOTgwMzklKSIsImZpbGxUeXBlNyI6ImhzbCgxODgsIDEwMCUsIDkzLjUyOTQxMTc2NDclKSJ9fSwidXBkYXRlRWRpdG9yIjpmYWxzZX0)](https://mermaid-js.github.io/mermaid-live-editor/#/edit/eyJjb2RlIjoic2VxdWVuY2VEaWFncmFtXG4gIHBhcnRpY2lwYW50IERlcGxveW1lbnRTeXN0ZW1cbiAgcGFydGljaXBhbnQgQUNSXG4gIHBhcnRpY2lwYW50IEF1dG9JbWFnZU1hbmFnZXJcbiAgcGFydGljaXBhbnQgRGljb21Db250cm9sbGVyXG4gIHBhcnRpY2lwYW50IERpY29tRGVwbG95bWVudFxuICBQYXJ0aWNpcGFudCBDb250cm9sbGVyTWFuYWdlclxuICBEZXBsb3ltZW50U3lzdGVtLT4-QUNSOiBQdXNoIG5ldyBpbWFnZVxuICBEZXBsb3ltZW50U3lzdGVtLT4-QXV0b0ltYWdlTWFuYWdlcjogU2V0IFNlcnZpY2VJbWFnZVxuXG4gIHJlY3QgcmdiYSgxMDUsIDEwNSwgMTA1LCAuMjUpXG4gICAgbm90ZSBvdmVyIEF1dG9JbWFnZU1hbmFnZXI6IFJlY29uY2lsZSBTZXJ2aWNlSW1hZ2UgKGFzeW5jKVxuICAgIEF1dG9JbWFnZU1hbmFnZXItPj5EaWNvbUNvbnRyb2xsZXI6IFVwZGF0ZSBpbWFnZTxici8-YWxsIERpY29tIENSRFxuICAgIERpY29tQ29udHJvbGxlci0-PkRpY29tRGVwbG95bWVudDogVXBkYXRlIFBvZCBTcGVjIGltYWdlXG4gICAgXG4gICAgcmVjdCByZ2JhKDEwNSwgMTA1LCAxMDUsIC4yNSlcbiAgICAgIG5vdGUgb3ZlciBDb250cm9sbGVyTWFuYWdlcjogRG8gcm9sbGluZyB1cGdyYWRlIChhc3luYylcbiAgICAgIENvbnRyb2xsZXJNYW5hZ2VyLT4-RGljb21EZXBsb3ltZW50OiBDcmVhdGUgUmVwbGljYVNldDxicj53L25ldyBJbWFnZVxuICAgICAgQ29udHJvbGxlck1hbmFnZXItPj5EaWNvbURlcGxveW1lbnQ6IENyZWF0ZSBwb2Q8YnI-aW4gbmV3IFJlcGxpY2FTZXRcbiAgICBEaWNvbURlcGxveW1lbnQtPj5BQ1I6IEdldCBuZXcgaW1hZ2VcbiAgICBBQ1ItPj5EaWNvbURlcGxveW1lbnQ6IE5ldyBpbWFnZVxuICAgICAgQ29udHJvbGxlck1hbmFnZXItPj5EaWNvbURlcGxveW1lbnQ6IFZhbGlkYXRlIFBvZCBIZWFsdGh5XG4gICAgICBDb250cm9sbGVyTWFuYWdlci0-PkRpY29tRGVwbG95bWVudDogRGVsZXRlIHBvZDxicj5pbiBvbGQgUmVwbGljYVNldFxuICAgICAgQ29udHJvbGxlck1hbmFnZXItPj5Db250cm9sbGVyTWFuYWdlcjogdW50aWwgb2xkIFJlcGxpY2FTZXQgZW1wdHlcbiAgICAgIERpY29tRGVwbG95bWVudC0-PkRpY29tQ29udHJvbGxlcjogVHJpZ2dlciByZWNvbmNpbGU8YnIvPihVcGRhdGVkIFN0YXR1cylcbiAgICBlbmRcbiAgICBEaWNvbUNvbnRyb2xsZXItPj5BdXRvSW1hZ2VNYW5hZ2VyOiBUcmlnZ2VyIHJlY29uY2lsZTxici8-KFVwZGF0ZWQgU3RhdHVzKVxuICAgIFxuICAgIHJlY3QgcmdiYSgxMDUsIDEwNSwgMTA1LCAuMjUpXG4gICAgICBub3RlIG92ZXIgQXV0b0ltYWdlTWFuYWdlcjogVXBkYXRlIFVwZ3JhZGUgU3RhdHVzXG4gICAgICBBdXRvSW1hZ2VNYW5hZ2VyLT4-RGljb21Db250cm9sbGVyOiBHZXQgU3RhdHVzXG4gICAgQXV0b0ltYWdlTWFuYWdlci0-PkF1dG9JbWFnZU1hbmFnZXI6IFJvbGx1cCBTdGF0dXNcbiAgICBlbmRcblxuICBlbmRcbiAgXG4gIHJlY3QgcmdiYSgxMDUsIDEwNSwgMTA1LCAuMjUpXG4gICAgbm90ZSBvdmVyIEFDUjogVmVyaWZ5IFVwZ3JhZGVcbiAgICBEZXBsb3ltZW50U3lzdGVtLT4-QXV0b0ltYWdlTWFuYWdlcjogUG9sbCBTdGF0dXNcbiAgICBEZXBsb3ltZW50U3lzdGVtLT4-RGVwbG95bWVudFN5c3RlbTogdW50aWwgZG9uZVxuICBlbmQiLCJtZXJtYWlkIjp7InRoZW1lIjoiZGVmYXVsdCIsInRoZW1lVmFyaWFibGVzIjp7ImJhY2tncm91bmQiOiJ3aGl0ZSIsInByaW1hcnlDb2xvciI6IiNFQ0VDRkYiLCJzZWNvbmRhcnlDb2xvciI6IiNmZmZmZGUiLCJ0ZXJ0aWFyeUNvbG9yIjoiaHNsKDgwLCAxMDAlLCA5Ni4yNzQ1MDk4MDM5JSkiLCJwcmltYXJ5Qm9yZGVyQ29sb3IiOiJoc2woMjQwLCA2MCUsIDg2LjI3NDUwOTgwMzklKSIsInNlY29uZGFyeUJvcmRlckNvbG9yIjoiaHNsKDYwLCA2MCUsIDgzLjUyOTQxMTc2NDclKSIsInRlcnRpYXJ5Qm9yZGVyQ29sb3IiOiJoc2woODAsIDYwJSwgODYuMjc0NTA5ODAzOSUpIiwicHJpbWFyeVRleHRDb2xvciI6IiMxMzEzMDAiLCJzZWNvbmRhcnlUZXh0Q29sb3IiOiIjMDAwMDIxIiwidGVydGlhcnlUZXh0Q29sb3IiOiJyZ2IoOS41MDAwMDAwMDAxLCA5LjUwMDAwMDAwMDEsIDkuNTAwMDAwMDAwMSkiLCJsaW5lQ29sb3IiOiIjMzMzMzMzIiwidGV4dENvbG9yIjoiIzMzMyIsIm1haW5Ca2ciOiIjRUNFQ0ZGIiwic2Vjb25kQmtnIjoiI2ZmZmZkZSIsImJvcmRlcjEiOiIjOTM3MERCIiwiYm9yZGVyMiI6IiNhYWFhMzMiLCJhcnJvd2hlYWRDb2xvciI6IiMzMzMzMzMiLCJmb250RmFtaWx5IjoiXCJ0cmVidWNoZXQgbXNcIiwgdmVyZGFuYSwgYXJpYWwiLCJmb250U2l6ZSI6IjE2cHgiLCJsYWJlbEJhY2tncm91bmQiOiIjZThlOGU4Iiwibm9kZUJrZyI6IiNFQ0VDRkYiLCJub2RlQm9yZGVyIjoiIzkzNzBEQiIsImNsdXN0ZXJCa2ciOiIjZmZmZmRlIiwiY2x1c3RlckJvcmRlciI6IiNhYWFhMzMiLCJkZWZhdWx0TGlua0NvbG9yIjoiIzMzMzMzMyIsInRpdGxlQ29sb3IiOiIjMzMzIiwiZWRnZUxhYmVsQmFja2dyb3VuZCI6IiNlOGU4ZTgiLCJhY3RvckJvcmRlciI6ImhzbCgyNTkuNjI2MTY4MjI0MywgNTkuNzc2NTM2MzEyOCUsIDg3LjkwMTk2MDc4NDMlKSIsImFjdG9yQmtnIjoiI0VDRUNGRiIsImFjdG9yVGV4dENvbG9yIjoiYmxhY2siLCJhY3RvckxpbmVDb2xvciI6ImdyZXkiLCJzaWduYWxDb2xvciI6IiMzMzMiLCJzaWduYWxUZXh0Q29sb3IiOiIjMzMzIiwibGFiZWxCb3hCa2dDb2xvciI6IiNFQ0VDRkYiLCJsYWJlbEJveEJvcmRlckNvbG9yIjoiaHNsKDI1OS42MjYxNjgyMjQzLCA1OS43NzY1MzYzMTI4JSwgODcuOTAxOTYwNzg0MyUpIiwibGFiZWxUZXh0Q29sb3IiOiJibGFjayIsImxvb3BUZXh0Q29sb3IiOiJibGFjayIsIm5vdGVCb3JkZXJDb2xvciI6IiNhYWFhMzMiLCJub3RlQmtnQ29sb3IiOiIjZmZmNWFkIiwibm90ZVRleHRDb2xvciI6ImJsYWNrIiwiYWN0aXZhdGlvbkJvcmRlckNvbG9yIjoiIzY2NiIsImFjdGl2YXRpb25Ca2dDb2xvciI6IiNmNGY0ZjQiLCJzZXF1ZW5jZU51bWJlckNvbG9yIjoid2hpdGUiLCJzZWN0aW9uQmtnQ29sb3IiOiJyZ2JhKDEwMiwgMTAyLCAyNTUsIDAuNDkpIiwiYWx0U2VjdGlvbkJrZ0NvbG9yIjoid2hpdGUiLCJzZWN0aW9uQmtnQ29sb3IyIjoiI2ZmZjQwMCIsInRhc2tCb3JkZXJDb2xvciI6IiM1MzRmYmMiLCJ0YXNrQmtnQ29sb3IiOiIjOGE5MGRkIiwidGFza1RleHRMaWdodENvbG9yIjoid2hpdGUiLCJ0YXNrVGV4dENvbG9yIjoid2hpdGUiLCJ0YXNrVGV4dERhcmtDb2xvciI6ImJsYWNrIiwidGFza1RleHRPdXRzaWRlQ29sb3IiOiJibGFjayIsInRhc2tUZXh0Q2xpY2thYmxlQ29sb3IiOiIjMDAzMTYzIiwiYWN0aXZlVGFza0JvcmRlckNvbG9yIjoiIzUzNGZiYyIsImFjdGl2ZVRhc2tCa2dDb2xvciI6IiNiZmM3ZmYiLCJncmlkQ29sb3IiOiJsaWdodGdyZXkiLCJkb25lVGFza0JrZ0NvbG9yIjoibGlnaHRncmV5IiwiZG9uZVRhc2tCb3JkZXJDb2xvciI6ImdyZXkiLCJjcml0Qm9yZGVyQ29sb3IiOiIjZmY4ODg4IiwiY3JpdEJrZ0NvbG9yIjoicmVkIiwidG9kYXlMaW5lQ29sb3IiOiJyZWQiLCJsYWJlbENvbG9yIjoiYmxhY2siLCJlcnJvckJrZ0NvbG9yIjoiIzU1MjIyMiIsImVycm9yVGV4dENvbG9yIjoiIzU1MjIyMiIsImNsYXNzVGV4dCI6IiMxMzEzMDAiLCJmaWxsVHlwZTAiOiIjRUNFQ0ZGIiwiZmlsbFR5cGUxIjoiI2ZmZmZkZSIsImZpbGxUeXBlMiI6ImhzbCgzMDQsIDEwMCUsIDk2LjI3NDUwOTgwMzklKSIsImZpbGxUeXBlMyI6ImhzbCgxMjQsIDEwMCUsIDkzLjUyOTQxMTc2NDclKSIsImZpbGxUeXBlNCI6ImhzbCgxNzYsIDEwMCUsIDk2LjI3NDUwOTgwMzklKSIsImZpbGxUeXBlNSI6ImhzbCgtNCwgMTAwJSwgOTMuNTI5NDExNzY0NyUpIiwiZmlsbFR5cGU2IjoiaHNsKDgsIDEwMCUsIDk2LjI3NDUwOTgwMzklKSIsImZpbGxUeXBlNyI6ImhzbCgxODgsIDEwMCUsIDkzLjUyOTQxMTc2NDclKSJ9fSwidXBkYXRlRWRpdG9yIjpmYWxzZX0)

## Usage

This is the expected flow of the resource worker that orchestrates the upgrade.

1. Set Image in ServiceImage instance
1. Sit in poll loop 
    1. Wait for generation == observedGeneration
        1. Failure to wait for this will mean we are looking at a previous result
        1. Filed issue - https://github.com/kubernetes/kubectl/issues/962 to allow kubectl to do this
    1. Wait for condition ServiceImageComplete
        1. `kubectl wait --for=condition=complete`

## Known Shortcomings


### What does it mean to be upgraded

Unfortunately it is impossible to guarantee that status "upgraded" means that no images are alive with a previous version. Due to this, it is questionable how much work to put into preventing various races with kubernetes on "upgrade done" and being upgraded.

My proposal is to **ALLOW** small races where after an upgrade has been done for a newly provisioned service to be created with the old image, which will then cause the upgrade for that tier to go back to pending. In which the upgrade will then proceed again to update the container to the new image.

#### Issues
1. A container can be on a node in kubernetes that has it's kubelet broken. This registers the node as dead. However does not mean that containers are not alive on it
1. "Upgrade done" can be made to mean that kubernetes will only launch new versions of the container (other than from races in nodes that are orphaned)
    1. It is questionable how valuable functionality is, due to a race existing where the old container can be alive
    1. We need to do a few things to provide this guarantee. (etcd is strongly consistent)
        * Every time we startup, we write an election ID onto the ServiceImage
            * This creates a fence to ensure we see any dicom objects created before our election 
        * Ensure newly created dicoms are ordered with respect to changes to ServiceImages
            * do this by changing updates to dicoms image(v1->v2) to the following
                1. Get dicom object (at time t1)
                1. Non cached read to get ServiceImage spec (at time t2)
                    1. Ensure Election ID is on ServiceImage for our process
                        1. This fence ensures that we are not processing any dicom objects created after we lost election
                1. Conditionally write(dicom @t1) dicom image is now v2
        * Ensure that when we update a ServiceImage status, that we conditionally write based on our electionID
            * This ensures that if another other process took over and decided to add newly created dicoms with the current image (v1, not v2) that we will fail to update the ServiceImage to "Upgrade done"
1. Changing a container between upgrade tiers
    * This introduces more confusion. It will also cause a return to "InProgress" for the upgrade tier
1. "Upgrade done" can be made to mean that kubernetes will not forward traffic to an image of an old version
    1. Unfortunately no generic way to interrogate ingress object to see potential containers that they forward to
        1. **TODO** investigate if service meshes offer something better here.

## Implementation

### Dicom CRD Updates Deployment

This is the workflow that occurs after a AutoImageManager has put a version on the ImageSpec. It is driven entirely by the DicomController

#### DicomController ReconcileLoop
1. Detect change in Dicom Instance or DeploymentObject
1. if necessary Update image to deployment
    1. Mark Upgrade in progress in status
1. Determine if Deployment has realized most recent update
    1. status contains observedGeneration that is >= Generation (why >= ? (old docs stated that))
        1. check "The Status of a Deployment" in link https://jamesdefabia.github.io/docs/user-guide/deployments/
        1. **Note** there is a bug in old documentation on how to determine if up to date
    1. If all replicas to be up to date (logical and of below conditions)
        * spec.replicas == status.replicas (we have the same number of replicas as we desire)
        * status.replicas == status.updatedReplicas (all the existing replicas are updated)
        * status.replicas == stats.availableReplicas (all the existing replicas are available)
        * all three checks are necessary because
            * we can have more than desired number of replicas
            * we may have old replicas that are available
            * updated replicas may not be available
    1. Mark Upgrade complete based on results of above

### AutoImageManager ReconcileLoop
1. Detect change in Dicom instance or ServiceImage
    1. Below operations can be cached to remove api server load
        1. assuming cache is primed with most recent change
    1. upgrade all dicom instances in priority level
        1. Loop through all upgrade tiers that map to priority level
            1. fetch all dicom instances that match upgrade tier
                1. run through list of matching CR's & update image if necessary
                    1. on failure requeue reconcile loop
                1. Update the status of the tier (and upgrade if necessary)
            1. If priority level is up to date, advance to next priority
                1. otherwise restart reconcile loop on next change
        1. Update status of Upgrade
    1. loop through all dicom instances and put initial version on any that do not have it

### Metrics

* OverAll Status
  * Number of instances at latest version
    * This number may decrease due to deletes
  * Number of crd's to be updated
    * To have a strictly decreasing number, ignore new deployments
    * Number may decrease due to deletes
  * is cluster being upgraded
  * how long has the cluster been upgraded/not upgrading
    * this will be using a timestamp since last time started/completed upgrade
  * Number of CRD's that are running un-managed
  * Number of CRD's that are running ignore failed
* Priority/UpgradeTier Status
  * current priority of update
  * how long since current priority start
    * this will be using a timestamp since last time current priority changed
  * Number of instances at latest version in current priority
    * This number may decrease due to deletes
  * Number of crd's to be updated current priority
    * To have a strictly decreasing number, ignore new deployments

### Tests

Most of the code is actually interfacing with kubernetes and is straight forward logic without much branching. Since it interfaces with an external component it will require end to end testing. Most portions of the code can be easily unit tested as well.
