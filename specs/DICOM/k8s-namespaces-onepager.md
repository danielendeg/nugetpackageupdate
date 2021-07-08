# Kubernetes Namespaces

Namespaces are virtual clusters backed by the same physical cluster.

## Features

Each namespace can manage its own:
- ResourceQuota
  - Can limit quantity by type and compute resources (CPU, memory, storage)
- RBAC
- NetworkPolicies
  - Restrict access in and out of pods
- Secrets
  - Can only be shared within the same namespace

## Current state

All resources are put in a single namespace, `default`, except for the dicom controller which is in `dicomoperator-system`.

## Options

| Change | Pros   | Cons |
| ------ | ------  | ------ |
| Keep everything in default (no change) | - No code changes needed | - All systems need to keep the same configurations.  - Unclear ownership across teams <br/> - No space for growth; with new services being added, it will be messy to keep in the same namespace |
| One namespace per component (DICOM, FHIR, IoMT) | - Each team owns their own service's configuration <br/> - Simple logical split / simple code change <br/> - Quotas & Policies can be set based on unique needs of each service <br/> - Allows for growth; new teams/services can create new namespaces  | - Could be difficult if one namespace needs to split into two, based on future team changes |
| One namespace per customer | - Can easily meet unique needs of customers (ex: set NetworkPolicies to refuse traffic from x) <br/> - Increased security for customers | - Similar features can be achieved using labels <br/> - Brings more complexity to the change <br/> - Unclear ownership of namespaces |
| Sub-namespaces <br/> (per customer namespaces within a component-level namespace) | - Can use shared quotas/policies on a team level for most things, and only set on customer level if needed | - Overly complex <br/> - Releases aren't stable yet. [HNC v0.8.0](https://github.com/kubernetes-sigs/multi-tenancy/releases/tag/hnc-v0.8.0) |

## Proposed change

One namespace per component (DICOM, FHIR, IoMT).

### Reasoning

We should start with the simplest configuration, changing minimal resources to achieve namespace separation. From there, we can re-evaluate if further separation should be done (ex: HNC). 

### Communication between namespaces

Pods in different namespaces can communicate with each other by adding the namespace to the DNS address. If desired, [Network Policies](https://kubernetes.io/docs/concepts/services-networking/network-policies/) or [Open Service Mesh](https://openservicemesh.io/) can be configured to restrict communication between namespaces.

### Dicom work

- Create dicom namespace `dicom`
- Create shared namespace for infrastructure `shared`
- Update namespace configurations:
  - `shared` namespace:
    - kured
    - geneva
    - aad pod identity controller
    - CSD secrets store controller
    - ingress controller
  - `dicomoperator-system` namespace
    - dicom controller
  - `dicom` namespace
    - dicom service instances
- Move existing dicom services into new namespace (TBD on how to do this)
- Update health-paas (migration & namespace changes) 

### Potential future work

- Add `ResourceQuota` and/or `NetworkPolicy` to `dicom` namespace
- Move ingress controller to live in the same namespace as the dicom controller
- Investigate adding sub-namespaces per customer under the `dicom` namespace [HNC](https://github.com/kubernetes-sigs/hierarchical-namespaces)
- Investigate putting kured into its own namespace [as suggested here](https://docs.microsoft.com/en-us/azure/aks/node-updates-kured#deploy-kured-in-an-aks-cluster).

### Unknowns

- If the ingress controller moves, should we rename `dicomoperator-system` to be more general? `dicom-infrastructure`?
- Name of namespace `dicom`, `dicom-platform`, `dicom-service`, `dicom-workspace`, etc?
- Name of shared namespace `shared-components`, `shared-infrastructure`?
- Separate namespace for dicom & dicom-cast?