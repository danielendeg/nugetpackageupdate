# Azure API for FHIR Service release requirements

The purpose of this document is to scope out all the necessary work needed to release Azure API for FHIR as an Azure Service. This includes:

- Billing Services and creating our billing meters
- Any outstanding work with CSS to support the service at GA time
- Localization work needed to be done in order to comply with Azure standards
- Compliance work needed for Release Tracker (Security Reviews, Privacy,…)
- Documentation
- Any other engineering work that needs to be done (ARM work, infrastructure optimization, Performance/Scaling,…)
- Geo expansion and deployment plan
- BCDR, GDPR and Privacy work

## Billing Services and custom meters

The way billing is currently implemented is, that we only charge customers for CosmosDB usage and set RU's at service creation time at 1000. This is currently hard coded in billing agent. For GA there are several items that needs to be done:

- Create new custom meters (infra + network + Cosmos DB?)
- Business planning around creating customer meters
- Query Commerce server for usage

|Task  |Eng Owner  |PM Owner  |Status  |
|---------|---------|---------|---------|
|Business Planning     |         |     Matjaz    |    On Track (waiting for new BP)     |
|Querying Commerce Server     |     Ganesh    |    Matjaz     |  On Track       |

## CSS Support

From the support perspective there is no additional work needed for GA. All processes that were setup for Public Preview are enough for GA.

|Task  |eng Owner  |PM Owner  |Status  |
|---------|---------|---------|---------|
|CSS Support onboarding     |   Joyce      |   Matjaz      |   On track/Completed      |

## Localization
As per [List of accepted languages](https://github.com/Azure/portaldocs/blob/master/portal-sdk/generated/portalfx-localization.md#list-of-accepted-languages) , Azure Portal should be localized in 18 languages (English + 17) by GA timeframe.


|Task     |Eng Owner  |PM Owner  |Status  |
|---------|---------|---------|---------|
|Azure portal Localization     |  Joyce       |  Matjaz       |  In progress/On track       |

Localization will start on 5/17, when we will do a first pass on localizing resource files. 

## Compliance

##SLA

