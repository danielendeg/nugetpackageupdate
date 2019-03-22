[[_TOC_]]

Ingest, search, and read performance are key metrics that we should know at all times and be able to communicate internally and to customers. We should aim to produce a FHIR service that performs well and it should be a differentiator for us. Consequently, we need to start tracking how our FHIR service performs using well understood configurations. For example, we should be able to say to a customer, if you set your Cosmos DB throughput to 10,000k RU/s you sould expect to be able to ingest about 500 resources per second or about 5000 patients (Synthea) per hour. 

# High-Level Design

Tests datasets:

* 10,000 Synthea patients
* 100,000 Synthea patients

KPIs:

* Cosmos DB throughput (ingest, read, search)
* Resources ingested per second
* Patients ingested per hour
* Total intest time for X patients
* For a given DB throughput, what is the frontend compute scale required. 


# Test Strategy

Describe the test strategy.

# Security

Describe any special security implications or security testing needed.

# Other

Describe any impact to localization, globalization, deployment, back-compat, SOPs, ISMS, etc.

