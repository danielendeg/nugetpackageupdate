# Azure API for FHIR - Region deployment and High Availability considerations

Currently Azure API for FHIR does not give customers option to configure High Availability (HA).
By the GA time we want to offer customer option to setup Managed service in HA configuration.
Combining region pairing and Ring requirements means that our service would need to be available in most Hero, Hubs and Satellite regions. This would inflate our infrastructure complexity and cost.
For Azure Region Strategy see [Azure Region Strategy](https://microsoft.sharepoint.com/teams/azureecosystem/servicerings/Shared%20Documents/K%20Rings/Azure%20Region%20Strategy.docx?web=1)

## Region pairing map

In order to offer HA of our service we need to specify what regions will we pair when customers selects this option during service provisioning. Current list of region pairing for Azure is at [https://docs.microsoft.com/en-us/azure/best-practices-availability-paired-regions](https://docs.microsoft.com/en-us/azure/best-practices-availability-paired-regions). When the customer will provisions new Azure API for FHIR service, we can point to the document, so they can understand what region gets paired automatically if they choose HA scenario.

Currently the service is deployed in **West US2, North Central US and UK West** region. In order to satisfy Ring 1 Requirements (28 regions) or Ring 2 requirements (10 Hero regions), we need to come up with geo expansion strategy of Azure API for FHIR at GA time.

### Azure Ring requirements 
Azure has different requirements for Services in different rings. Project Resolute started as Ring 2 service, but has been moved to Ring 1 at the time of Public Preview. This puts some stricter requirements on where service needs to be deployed and available. 
This is documented in [Microsoft Cloud Rings](https://microsoft.sharepoint.com/teams/azureecosystem/servicerings/Shared%20Documents/Sc%20Rings/Microsoft%20Cloud%20Rings%20-%20Scandium.docx?web=1)

Ring 2 promise:
* Cloud expansion: **Hero Regions**
* Customer Data Management: Customer data resides in Geo & is GDPR compliant
* Assurance and Compliance: Foundational certifications (ISO, SOC, PCI, FedRAMP**)
* Resiliency and High Availability: **No promise**
* Lifecycle Commitment: 12-month product deprecation or change notice

Ring 1 promise:
* Cloud expansion: **Hub & Hero Regions**
* Customer Data Management: Customer data resides in Geo & is GDPR compliant
* Assurance and Compliance: Foundational certifications (ISO, SOC, PCI, FedRAMP**)
* Resiliency and High Availability: **Zone Aware, Failover promise, & No customer data loss**
* Lifecycle Commitment: 12-month product deprecation or change notice

### Ring 2 pairing

Ring 2 requires us to be in all Hero regions withing 30 days of GA. To satisfy Azure requirements and offering HA in US, Europe and UK, we need to be deployed in following regions:

| Region  |Type|Region|Type  | HA Available |
|-------|------|------|----|----|
|East US (Public / United States)|Hero | ||No|
|East US 2 (Public / United States)|Hero| ||No |
|**North Central US (Public / United States)*** |Hub|South Central US (Public / United States)|Hero|Yes |
|**West US 2 (Public / United States)** |Hero| ||No |
|West Europe (Public / Europe) |Hero|North Europe (Public / Europe) |Hero|Yes|
|UK South (Public / United Kingdom) |Hero|**UK West (Public / United Kingdom)*** |Satellite|Yes |
|Australia East (Public / Australia) |Hero | ||No|
|Southeast Asia (Public / Asia Pacific) |Hero | ||No |
|Sweden Central (Public / Sweden) |Hero|||No |

***Note**: UK West (Public / United Kingdom) and North Central US (Public / United States)  are Satellite/Hub regions, but we are already deployed there and use them for HA.

Based on current service footprint we need to deploy to additional **9 regions** to satisfy Ring 2 requirements. If we want to offer HA in every region then we need to deploy in additional **15 regions** from where we are today.

### Ring 1 pairing

Currently Azure API for FHIR sits in Ring 1 (moved from Ring 2). Current Azure requirements are that ring 1 services are in all Hero regions within 30 days of GA and 180 days in Hub regions. 

|Regions  |Type|Region | Type| HA Available |
|-------|------|-----|-----|----|
|North Europe (Public / Europe) |Hero|West Europe (Public / Europe)| Hero| Yes|
|East US (Public / United States)|Hero|West US (Public / United States) |Hub|Yes|
|East US 2 (Public / United States)|Hero|Central US (Public / United States)|Hub| Yes|
|**North Central US (Public / United States)*** |Hub|South Central US (Public / United States) |Hero|Yes|
|Canada Central (Public / Canada)|Hub|||No|
|**West US 2 (Public / United States)***|Hero|||No|
|UK South (Public / United Kingdom)|Hero|**UK West (Public / United Kingdom)*** |Satellite|Yes|
|France Central (Public / France) |Hub|||No|
|Germany West Central (Public / Germany)|Hub|| |No|
|Switzerland North (Public / Switzerland)|Hub|| |No|
|Norway East (Public / Norway)|Hub|||No|
|Sweden Central (Public / Sweden)|Hero|||No|
|East Asia (Public / Asia Pacific)|Hub|Southeast Asia (Public / Asia Pacific)|Hero|Yes|
|Brazil South (Public / Brazil)|Hub|||No|
|Australia East (Public / Australia)|Hero|||No|
|Japan East (Public / Japan)|Hub|||No|
|Korea Central (Public / Korea)|Hub|||No|
|Central India (Public / India)|Hub|||No|
|South Africa North (Public / South Africa)|Hub|||No|
|UAE North (Public / UAE)|Hub|||No|

***Note**: UK West (Public / United Kingdom) is Satellite region, but we are already deployed there and use them for HA.

Based of current service footprint we need to deploy to additional **23 regions** to satisfy Ring 2 requirements. This is valid is we don't offer HA in every region. If that was the case we need to deploy in additional **37 regions** from where we are today.

### Billing impact

Current billing impact for a customer that enables HA on their instance is, that the cost doubles as we instantiate a new Cosmos DB account in paired region and replicate the data between the regions.

## Geo-expansion rollout plan

Geo expansion plan needs to be planned in  [PAM tool](https://global.azure.com/product-availability/availability-by-offering/offering/1461)

For Ring 1 service we need to be in all Hero regions by GA + 30 days and Hub regions by GA + 180. 
For Ring 2 service we need to be in all Hero regions by GA + 30 days.

|Priority|Region|Type|Deployment date (GA)|Notes|
|----|----|----|----|----|
|1|West Europe|Hero|Aug 2019||
|2|East US|Hero|Aug 2019||
|3|Australia East|Hero|Sept 2019||
|4|Southeast Asia|Hero|Sep 2019||
|5|Central US EUAP (Canary)|Satellite|Sep 2019|Canary region|
|6|UK South|Hero|Aug 2019||
|7|North Europe|Hero|Aug 2019||
|8|East US 2|Hero|Aug 2019||
|9|South Central US|Hero|Aug 2019||
|10|**West US 2**|Hero|Nov 2018||
|11|West US|Hub|Aug 2019||
|12|Central US|Hub|Aug 2019||
|13|**North Central US**|Hub|Nov  2018|
|14|Japan East|Hub|Feb 2020|
|15|East Asia|Hub|Feb 2020||
|16|USGov Virginia|Hub|Dec 2019|UsGov|
|17|Canada Central|Hub|Feb 2020||
|18|China East 2|Hub|TBD|Mooncake|
|19|Central India|Hub|Feb 2020||
|20|Korea Central|Hub|Feb 2020||
|21|France Central|Hub|Sep 2019||
|22|South Africa North|Hub|Feb 2020||
|23|UAE North|Hub|Feb 2020|
|24|Switzerland North|Hub|Feb 2020||
|25|Germany West Central|Hub|Feb 2020||
|26|Brazil South|Hub|Feb 2020||
|27|Norway East|Hub|Feb 2020|
|28|Sweden Central|Hero|September 2019||
|29|**UK West**|Satellite|Nov 2018||






