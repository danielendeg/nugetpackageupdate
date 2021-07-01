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

All resources are put in a single namespace, `default`

## Options

| Change | Pros   | Cons |
| ------ | ------  | ------ |
| Keep everything in default (no change) | - No code changes needed | - All systems need to keep the same configurations.  - Unclear ownership across teams <br/> - No space for growth; with new services being added, it will be messy to keep in the same namespace |
| One namespace per team (DICOM, FHIR, IoMT) | - Each team owns their own service's configuration <br/> - Simple logical split / simple code change <br/> - Quotas & Policies can be set based on unique needs of each service <br/> - Allows for growth; new teams/services can create new namespaces  | - Could be difficult if one namespace needs to split into two, based on future team changes |
| One namespace per customer | - Can easily meet unique needs of customers (ex: set NetworkPolicies to refuse traffic from x) | - Same can be achieved using labels <br/> - Brings more complexity to the change <br/> - Unclear ownership of namespaces |
| Sub-namespaces <br/> (per customer namespaces within a team-level namesapce) | - Can use shared quotas/policies on a team level for most things, and only set on customer level if needed | - Over-complicated for little gain <br/> - Releases aren't stable yet. [HNC v0.8.0](https://github.com/kubernetes-sigs/multi-tenancy/releases/tag/hnc-v0.8.0) |

## Proposed change

One namespace per team (DICOM, FHIR, IoMT).

### Communication between namespaces

Pods in different namespaces can communicate with eachother by adding the namespace to the DNS address. If desired, [Network Policies](https://kubernetes.io/docs/concepts/services-networking/network-policies/) can be set to restrict communication between namespaces.

### Dicom work

- Create namespace `dicom`
- Update dicom yaml
- Update infrastructure to deploy in different namespaces
- Update `V1Alpha1Dicom` to include namespace
- Add `ResourceQuota` and/or `NetworkPolicy` for namespace if desired
- Move existing dicom services into new namespace (TBD on how to do this)

### Unknowns

- Name of namespace `dicom`, `dicom-platform`, `dicom-service`, `dicom-workspace`, etc?
- Separate namespace for dicom & dicom-cast?
- Any changes in health-pass?


