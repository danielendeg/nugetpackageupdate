TODO : Merge this with /specs/Jupiter/PrivateLink.md when checkin is complete

*Private Link Capabilities for DICOM*
The ability to talk to DICOM instances over a private ip (only reachable by the customer) in Azure.

[[_TOC_]]

## Justificiation
Private link prevents certain data exfiltration scenarios. It also can lower Azure COGS by needing less public IPv4 ip addresses. It is seen as more secure, and is seen as necessary from customers.

# Design

The goal is to leverage existing code scenarios built for the [FHIR scenarios](https://microsofthealth.visualstudio.com/Health/_git/health-paas-docs/pullrequest/14950?path=%2Fspecs%2FJupiter%2FPrivateLink.md&_a=files). 

## FHIR capabilities / Existing Service Fabric infra

1. Book keeping associated with private link
    1. Ability to mark the workspace as belonging to a Private Link.
    1. Ability to add and remove private link endpoints.
    1. Ability to update DNS records
1. Ability to decap TLS sessions (for FHIR)
1. Ability to validate host, sni, & private link endpoint.
1. Frontend that handles off of this and then proxies to backend for processing
1. Monitoring for ingress cluster (Service Fabric)

## GAPS for DICOM

1. Traffic needs to be identified & proxied to a DICOM backend, not a service fabric cluster
    1. Traffic needs to be secured (mTLS certificate)
1. Private mTLS Certs need to be created / rotated
1. ~~[Appropriate certs](https://microsofthealth.visualstudio.com/Health/_search?action=contents&text=.privatelink&type=code&lp=code-Project&filters=ProjectFilters%7BHealth%7DRepositoryFilters%7Bhealth-paas%7D&pageSize=25&result=DefaultCollection%2FHealth%2Fhealth-paas%2FGBmaster%2F%2Fdeployment%2FNew-CertificateVersion.ps1) for DICOM private link need to be registered in service fabric frontend~~
    1. ~~allows decap of the appropriate domain `*.dicom.<?:privatelink>.azurehealthcareapis.com`~~
1. Add configuration mapping from Service Fabric to AKS cluster
    1. Need to expose endpoint from Service Fabric to AKS cluster.
        1. probably should use DNS, fronted via traffic manager
            1. `AvailabilityZone-Region-aks.internal.mshapis.com`
                1. EX: `0-westus2-aks.internal.mshapis.com`
            1. **TODO** Determine what internal url we have
1. Need to add AKS ev2 setup
    1. Configure incoming load balancer & extra ingress endpoint
        1. Consider moving endpoint to private network
        1. use keyvault-csi to do auto loading of cert
1. Need to add RP code handling for DICOM to update AKS cluster instance between public & private
    1. Need private link interface for RP worker for dicom
    1. Need to call AKS - Dicom CRD & update is private link
    1. Need to update traffic manager to switch from public endpoint to private endpoint.
1. Need to add DICOM controller code to handle switching to from public to private link
1. Need to add ability to securely send traffic internally in AKS cluster.
    1. Easiest way to solve this is through use of Service Mesh. 
        1. Currently talking to OSM team in Public preview if we can take dependency on them.
            1. Worked with OSM team about [Adding support for ingress & mTLS](https://github.com/openservicemesh/osm/issues/3582)
                1. Fix is now in main & release .9.1 will be cut with the changes
1. Need to update e2e test for private link & workspace.

## Pictures
Solid lines are traffic flows, dashed lines are retrieval of information.

[![](https://mermaid.ink/img/eyJjb2RlIjoiZ3JhcGggVERcbiAgICBwdWJbUHVibGljIENsaWVudF0tLT4gfCBwdWJsaWMgVExTIHwgcHViSVxuICAgIHB1YltQdWJsaWMgQ2xpZW50XSAtLi0-IHxHZXQgcHVibGljIEFLUyBFbmRwb2ludHwgcHViVE1bVHJhZmZpYyBNYW5hZ2VyXVxuICAgIHByb3h5W3ByaXZhdGUgbGluayBwcm94eV0gLS4tPiB8IEdldCBpbnRlcm5hbCBtVExTfEtleVZhdWx0XG4gICAgcHJveHkgLS4tPiB8IEdldCBwcml2YXRlIFRMU3xLZXlWYXVsdFxuICAgIHByb3h5IC0uLT4gfCBHZXQgcHJpdmF0ZSBBS1MgRW5kcG9pbnR8IHRtW1RyYWZmaWMgTWFuYWdlcl1cbiAgICBwcm94eSAtLT58IGRlY2FwIHByaXZhdGUgVExTIC0gZm9yd2FyZCB3L2ludGVybmFsIG1UTFN8IHByaXZJXG4gICAgYWNjb3VudCAtLi0-IHwgR2V0IHByaXZhdGUgbGluayBpZC9ob3N0IG1hcHBpbmd8IGdEQltnbG9iYWwgREJdXG4gICAga2V5dmF1bHRDU0lbS2V5VmF1bHQgQ1NJXSAtLi0-IHwgR2V0IGludGVybmFsIG1UTFN8S2V5VmF1bHRcbiAgICBrZXl2YXVsdENTSVtLZXlWYXVsdCBDU0ldIC0uLT4gfCBHZXQgcHVibGljIFRMU3xLZXlWYXVsdFxuICAgIHN1YmdyYXBoIEFLU1xuICAgICAgICBwdWJJW1B1YmxpYyBJbmdyZXNzIEVuZHBvaW50XSAtLT4gfCBwdWJsaWMgVExTIHxuZ2lueFtQcml2YXRlIE5HSU5YIEluZ3Jlc3NdXG4gICAgICAgIHByaXZJW1ByaXZhdGUgSW5ncmVzcyBFbmRwb2ludF0gLS0-IHwgZm9yd2FyZCBwcml2YXRlIG1UTFMgfG5naW54W1B1YmxpYyBOR0lOWCBJbmdyZXNzXVxuICAgICAgICBzdWJncmFwaCBEaWNvbSBQb2RcbiAgICAgICAgICAgIGVudm95RGljb21bRW52b3ldIC0tPiB8IGRlY2FwIHNlcnZpY2UgbWVzaCBtVExTIHwgRElDT01cbiAgICAgICAgZW5kXG4gICAgICAgIHN1YmdyYXBoIEluZ3Jlc3MgQ29udHJvbGxlciBQb2RcbiAgICAgICAgICAgIG5naW54W0luZ3Jlc3MgQ29udHJvbGxlcl0gLS0-IHwgZGVjYXAgaW50ZXJuYWwgbVRMUyAmIHB1YmxpYyBUTFMgfGVudm95TmdpbnhbT1NNIEVudm95XVxuICAgICAgICAgICAga2V5dmF1bHRDU0lbS2V5VmF1bHQgQ1NJXSBcbiAgICAgICAgICAgIG5naW54W0luZ3Jlc3MgQ29udHJvbGxlcl0gLS4tPiB8IEdldCBDZXJ0cyB8IGtleXZhdWx0Q1NJW0tleVZhdWx0IENTSV1cbiAgICAgICAgICAgIGVudm95TmdpbnhbU2VydmljZSBNZXNoIEVudm95XSAtLT4gIHwgc2VydmljZSBtZXNoIG1UTFMgfGVudm95RGljb21bU2VydmljZSBNZXNoIEVudm95XVxuICAgICAgICBlbmRcbiAgICBlbmRcbiAgICBjW1ByaXZhdGUgQ2xpZW50XSAtLT4gfCBwcml2YXRlIFRMUyB8cEVQW1ByaXZhdGUgTGluayBFbmRwb2ludF1cbiAgICBwRVAgLS0-IHwgcHJpdmF0ZSBUTFMgfHByb3h5XG4gICAgc3ViZ3JhcGggU2VydmljZSBGYWJyaWNcbiAgICAgICAgcHJveHkgLS4tPiB8IFZhbGlkYXRlIHByaXZhdGUgbGlua3wgYWNjb3VudFtBY2NvdW50IFJvdXRpbmddXG4gICAgcHJveHlcbmVuZFxuIiwibWVybWFpZCI6eyJ0aGVtZSI6ImRlZmF1bHQifSwidXBkYXRlRWRpdG9yIjpmYWxzZSwiYXV0b1N5bmMiOnRydWUsInVwZGF0ZURpYWdyYW0iOmZhbHNlfQ)](https://mermaid-js.github.io/mermaid-live-editor/edit##eyJjb2RlIjoiZ3JhcGggVERcbiAgICBwdWJbUHVibGljIENsaWVudF0tLT4gfCBwdWJsaWMgVExTIHwgcHViSVxuICAgIHB1YltQdWJsaWMgQ2xpZW50XSAtLi0-IHxHZXQgcHVibGljIEFLUyBFbmRwb2ludHwgcHViVE1bVHJhZmZpYyBNYW5hZ2VyXVxuICAgIHByb3h5W3ByaXZhdGUgbGluayBwcm94eV0gLS4tPiB8IEdldCBpbnRlcm5hbCBtVExTfEtleVZhdWx0XG4gICAgcHJveHkgLS4tPiB8IEdldCBwcml2YXRlIFRMU3xLZXlWYXVsdFxuICAgIHByb3h5IC0uLT4gfCBHZXQgcHJpdmF0ZSBBS1MgRW5kcG9pbnR8IHRtW1RyYWZmaWMgTWFuYWdlcl1cbiAgICBwcm94eSAtLT58IGRlY2FwIHByaXZhdGUgVExTIC0gZm9yd2FyZCB3L2ludGVybmFsIG1UTFN8IHByaXZJXG4gICAgYWNjb3VudCAtLi0-IHwgR2V0IHByaXZhdGUgbGluayBpZC9ob3N0IG1hcHBpbmd8IGdEQltnbG9iYWwgREJdXG4gICAga2V5dmF1bHRDU0lbS2V5VmF1bHQgQ1NJXSAtLi0-IHwgR2V0IGludGVybmFsIG1UTFN8S2V5VmF1bHRcbiAgICBrZXl2YXVsdENTSVtLZXlWYXVsdCBDU0ldIC0uLT4gfCBHZXQgcHVibGljIFRMU3xLZXlWYXVsdFxuICAgIHN1YmdyYXBoIEFLU1xuICAgICAgICBwdWJJW1B1YmxpYyBJbmdyZXNzIEVuZHBvaW50XSAtLT4gfCBwdWJsaWMgVExTIHxuZ2lueFtQcml2YXRlIE5HSU5YIEluZ3Jlc3NdXG4gICAgICAgIHByaXZJW1ByaXZhdGUgSW5ncmVzcyBFbmRwb2ludF0gLS0-IHwgZm9yd2FyZCBwcml2YXRlIG1UTFMgfG5naW54W1B1YmxpYyBOR0lOWCBJbmdyZXNzXVxuICAgICAgICBzdWJncmFwaCBEaWNvbSBQb2RcbiAgICAgICAgICAgIGVudm95RGljb21bRW52b3ldIC0tPiB8IGRlY2FwIHNlcnZpY2UgbWVzaCBtVExTIHwgRElDT01cbiAgICAgICAgZW5kXG4gICAgICAgIHN1YmdyYXBoIEluZ3Jlc3MgQ29udHJvbGxlciBQb2RcbiAgICAgICAgICAgIG5naW54W0luZ3Jlc3MgQ29udHJvbGxlcl0gLS0-IHwgZGVjYXAgaW50ZXJuYWwgbVRMUyAmIHB1YmxpYyBUTFMgfGVudm95TmdpbnhbT1NNIEVudm95XVxuICAgICAgICAgICAga2V5dmF1bHRDU0lbS2V5VmF1bHQgQ1NJXSBcbiAgICAgICAgICAgIG5naW54W0luZ3Jlc3MgQ29udHJvbGxlcl0gLS4tPiB8IEdldCBDZXJ0cyB8IGtleXZhdWx0Q1NJW0tleVZhdWx0IENTSV1cbiAgICAgICAgICAgIGVudm95TmdpbnhbU2VydmljZSBNZXNoIEVudm95XSAtLT4gIHwgc2VydmljZSBtZXNoIG1UTFMgfGVudm95RGljb21bU2VydmljZSBNZXNoIEVudm95XVxuICAgICAgICBlbmRcbiAgICBlbmRcbiAgICBjW1ByaXZhdGUgQ2xpZW50XSAtLT4gfCBwcml2YXRlIFRMUyB8cEVQW1ByaXZhdGUgTGluayBFbmRwb2ludF1cbiAgICBwRVAgLS0-IHwgcHJpdmF0ZSBUTFMgfHByb3h5XG4gICAgc3ViZ3JhcGggU2VydmljZSBGYWJyaWNcbiAgICAgICAgcHJveHkgLS4tPiB8IFZhbGlkYXRlIHByaXZhdGUgbGluayB8IGFjY291bnRbQWNjb3VudCBSb3V0aW5nXVxuICAgIHByb3h5XG5lbmRcbiIsIm1lcm1haWQiOiJ7XG4gIFwidGhlbWVcIjogXCJkZWZhdWx0XCJcbn0iLCJ1cGRhdGVFZGl0b3IiOmZhbHNlLCJhdXRvU3luYyI6dHJ1ZSwidXBkYXRlRGlhZ3JhbSI6ZmFsc2V9)



## Scaling

All traffic flows will need to balance and route based on CPU & networking utilization.

### Limitations
1. CPU
    1. This is largely in response to encryption,
        1. decap TLS session
        1. encap mTL session
1. Network bandwidth.
    1. All VMs have network bandwidth constraints
1. Other resources
    1. Disk / memory should not be an issue.

## Other implementations
See https://microsoft.sharepoint.com/:o:/r/teams/Aznet/_layouts/15/Doc.aspx?sourcedoc=%7B43c88cdf-0864-4993-a4a8-fb0770bd39ef%7D&action=view&wd=target(Documentation.one%7Cab1cc159-9ace-422c-9822-b902d233f61f%2FBuilding%20a%20Proxy%20for%20your%20data%20plane%7C7d652877-9e04-490e-98f7-623474ca37c1%2F) 

### Comparison AzureML implementation
1. Use VMSS instead of Service Fabric cluster [PR](https://nam06.safelinks.protection.outlook.com/?url=https%3A%2F%2Fmsdata.visualstudio.com%2FVienna%2F_git%2Fkubernetes-util%2Fpullrequest%2F324463&data=02%7C01%7Cmalop%40microsoft.com%7C219daab28e8f48bcca2408d7dce1bd5d%7C72f988bf86f141af91ab2d7cd011db47%7C1%7C0%7C637220734411440221&sdata=X6nHb0aEI8SeaV0VkCLf%2FnjglEBr1LeEFQxTK8MIxks%3D&reserved=0)
    1. Easy to "scale" just set up VMSS scale and machines will scale appropriate.
1. Do not validate traffic at front end (VMSS).
    1. Simply decap TLS session, add IPv6 address Header, and forward to AKS cluster
    1. AKS cluster reads IPv6 address & validates private link & host name with auth endpoint
        1. Auth Endpoint is .net app that caches lookup entries in cosmosdb & validates private link id & hostname
1. Have their own manually configured mTLS inside cluster

### Downsides of AzureML implementation
Based on costs of below I do not think we should persue this path

1. Monitoring
    1. Another set of things to monitor and ensure they are working correctly.
1. Compliance
    1. You have to keep it up to date, figure out how to set it up correctly
1. Integration
    1. would need to integrate with our existing DICOM RP
