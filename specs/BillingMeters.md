# Billing meters for Azure API for FHIR

Azure API as managed azure service needs to bill customers for service usage. The purpose of this document is to describe the billing meters that service emits into customer subscription.

[[_TOC_]]

# Business Justification

In order to generate revenue Azure API for FHIR needs to define its own business model on how it will charge customers for consuming the service. As service uses several underlying Azure services that get charged to us, we need to pass some of the charges to the customers plus add some charge on top to cover the cost of running infrastructure, compliance work and other services that enable our service to operate

# Scenarios

Following are the scenarios that we want to bill customers for our service usage:

* Pass through to customer subscription:
    - Cosmos DB RU usage
    - Cosmos DB Storage usage
    - Network egress for replication between regions if Geo-Redundancy is enabled
    - Network egress for traffic from service to the internet
* Custom meter
    - Azure API for FHIR /h charge for using the service. We start charging this meter after service is created and then keep emitting

# Metrics


# Design

## Cosmos DB
When emitting meeters in billing agent, we will emit two Cosmos DB specific meeters. Every hour we will emit RU meeter GUID for the amount of RU that is provisioned on Cosmos DB(unit is 100 RU) into customer subscription. On top of that we will also emit Cosmos DB storage usage to customer subscription.

For Azure API for FHIR per hour usage, we will emit our meter GUID to customer subscription for every hour of service use for every scale unit. If customer had Geo-Redundancy setting, we will emit charge for every account in another paired region.

In case customer uses more than one scale unit of service (performance reason), we will emit factor of that to the customer subscription. For example if customer has two scale units of service running in our cluster, we will emit two meeters per hour to their subscription.

On top of that we will emit network egress meter with the amount of data consumed in region to region traffic and network traffic from service to the internet.

# Test Strategy



# Security



# Other

N/A
