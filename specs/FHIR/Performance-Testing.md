[[_TOC_]]

Ingest, search, and read performance are key metrics that we should know at all times and be able to communicate internally and to customers. We should aim to produce a FHIR service that performs well and it should be a differentiator for us. Consequently, we need to start tracking how our FHIR service performs using well understood configurations. For example, we should be able to say to a customer, if you set your Cosmos DB throughput to 10,000k RU/s you sould expect to be able to ingest about 500 resources per second or about 5000 patients (Synthea) per hour. 

# High-Level Design

The performance testing will be based on ingesting, querying, and retrieving synthetic patient datasets generated with Synthea. There are two critical areas of testing: 1) How much compute is needed for a given backend throughout, 2) With optimally configured front-end and back-end, how long would it take to ingest or read certain datasets. 

# Concept and Definitions

In the tests defined below we will use the following terms:

* Database throughput:
    * Cosmos DB: Provisioned RU/s
    * SQL: TBD
* Frontend compute: Amount of compute provisioned on the frontend. It makes sense to define a scale unit, e.g. 500 millicores (or ACUs) and 1 GB RAM. **TODO**: Define scale unit. Ideally, this would map to our billing too. 


# Test Strategy

## The Test Setup

1. A loader application. A good starting point would be the [FhirImporter](https://github.com/Microsoft/fhir-server-samples/tree/master/src/FhirImporter), which **should** be deployed in a container instance (or Kubernetes) to avoid performance issues with Function apps running in App Service. 
1. An instance (or multiple instances) of the PaaS FHIR service running in the DogFood environment. We should avoid testing against a server running in App Service as there are potential bottle necks. An alternative would be to use a Kubernetes based deployment where we could have easier control of the amount of compute provisioned. **TODO**: Decide on a reproducible setup which will allow control of backend and frontend scaling in both PaaS and for our upstream (OSS build).

## Test Flow

1. Establish the required compute for a given DB throughput. The thought would be to provision a database with relatively low throughput (e.g. 1000 RUs) and establish the minimum compute (minimum scale units) that would allow us to saturate the backend. Essentially we would have to titrate our way to the appropriate amount of compute. We should track this number to detect any increase (or decrease) in required compute and for the purpose of business modeling and deployment planning. 
1. For a select set of database throughput configurations (e.g. 1,000 RUs, 10,000 RUs, 25,000 RUs), run the following tests:
    1. Ingest known 10,000 Synthea patient lives.
    1. Ingest known 100,000 Synthea patient lives (only for higher DB throughput, e.g. > 10k RUs).
    1. Perform pre-defined search queries (e.g all systolic blood pressure observations above 140mmHg)
    1. Extract (read) all Patients, Observations, Conditions

## Reported results

As we get familiar with the KPIs and what we would like to monitor, we will expand on the results we report, but initially we should have reports of:

1. Graph of front end compute vs backend database throughput (for SQL and Cosmos)
1. Histograms of time it takes to complete API calls.
1. Average, peak, percentiles CPU and memory consumption during tests.
1. Performance table:

| Test case                                                                                | 1k RU  | 10k RU  | 25k RU  
|------------------------------------------------------------------------------------------|--------|---------|---------
| Ingest 10k patients                                                                      |  x min |  y min  |  z min  
| Ingest 100k patients                                                                     |  N/A   |  y min  |  z min  
| `/Observation?component-code-value-quantity=http://loinc.org|8480-6$lt60` @10k patients  |  x ms  |  y ms   |  z ms
| `/Observation?component-code-value-quantity=http://loinc.org|8480-6$lt60` @100k patients |  x ms  |  y ms   |  z ms
| `/Some other query                                                      ` @10k patients  |  x ms  |  y ms   |  z ms
| `/Some other query                                                      ` @100k patients |  x ms  |  y ms   |  z ms
| Export (read) 10k patients (Patient, Observation, Condition, Encounter)                  |  N/A   |  y min  |  z min  
| Export (read) 100k patients (Patient, Observation, Condition, Encounter)                 |  x min |  y min  |  z min  

**Note**: Some tests will be skipped for low RUs. They would simply be too slow.

**Todo**: Add additional tests, e.g. for Patient compartment and clarify what we are measuring; time for first page, time for all pages, etc. Let's start with a few tests, have a first "report" and then adjust and add tests.

We could consider having an App Insights/Log Analytics instance with telemetry from the test run as an artifact that we could go back to and generate other views, e.g. representative curves of RUs consumed vs time, number of 429s over time, etc. It could be costly to maintain this, so we should think about good ways of archiving, etc. We could direct all test telemetry to existing instances and simply let the data age off. The test report should then contain a clear table with timings of the individual tests.

# Phased approach

Performance testing is clearly something that will evolve over time as we identify cases we want keep track off. Here is a phased approach:

1. Test setup:
    1. Create Kubernetes manifests and AKS template to automate test environment provisioning:
        1. Create frontend container.
        1. Use Open Service Broker for Azure for DB.
    1. Create test data:
        1. 1k, 10k, 100k Synthea patients in storage accounts.
    1. Create loader app provisioning in cluster.
    1. Determine frontend scaling for 1k RUs, 10k RUs, 25k RUs.
    1. Measure ingest time for 1k patients, 10k patients, 100k patients.
1. Add automation for test cases:
    1. Ingests
    1. Searches
    1. Add initial search use cases.
    1. Ingest data and perform searches and generate reports.
1. Add manifests/templates for HAPI.
    1. Comparative testing.
1. Add manifest/templates for Vonk.
    1. Comparative testing.

# Future tests

We will add tests to this as we expand the capabilities of the service. Specifically, we will need tests for:

1. Validation (of Profiles)
1. Transactions
1. Bulk `$export`
1. RBAC

# Competitive comparisons

We should know how our service stacks up agains HAPI FHIR and Vonk (and possibly others, e.g. Google Healthcare APIs). Once the initial KPIs and reportings have been established, this should be expanded to include a table such as the one above for each of the competitive offerings.
