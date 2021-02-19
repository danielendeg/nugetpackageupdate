*Summary of the feature.*

[[_TOC_]]

# Business Justification

Customers want to be able to sort their search results using the `_sort` query parameter.

# Scenarios

1. Enable search requests using `_sort` for fhir-servers backed by SQL datastore. Initial support will be for string type search values and sorting by a single parameter. Later we will expand to supporting other search types and sorting by multiple parameters.

# Metrics

1. Accounts that have `_sort` enabled (by using the AccountTelemetry data).
2. Request metrics for requests that include the `_sort` parameter.

# Design

_Note: This is more like a draft design document. It is expected that this will evolve as development work on this feature progresses._

## Background information on existing search infrastructure

The SQL data store is setup in such a way that we have one table for each search parameter type (Eg: `dbo.StringSearchParam`, `dbo.NumberSearchParam`). We also have a `dbo.Resource` table where we store the actual resource along with some metadata. Whenever we upsert a resource, we extract all the search parameter values and add them to the corresponding search parameter tables. When we search for resources based on a search parameter, we do a query on the corresponding search parameter table and then select the corresponding resources from the `dbo.Resource` table (This is a simplified overview. For more details you can refer to the documentation on the [SQL Data Provider](./SQL-Server-Data-Provider-in-OSS.md)).

## Expected behavior for _sort

There are two major challenges we need to address with respect to supporting `_sort`:

1. Some search parameters can have multiple values (eg: `name` for `Patient`). The FHIR spec defines the following behaviour for such parameters:

    ```json
    A search parameter can refer to an element that repeats, and therefore there can be multiple values for a given search parameter for a single resource. In this case, the sort is based on the item in the set of multiple parameters that comes earliest in the specified sort order when ordering the returned resources
    ```

2. Resources might not have values for certain paramters. Eg: Some `Patient` resources might not have the `address-city` data. When sorting for Patients by `address-city` we should make sure we return these Patients also. The expected behavior for such situations is to have the resources which do not have `address-city` data to show up at the top of the list for ascending sort order and vice-versa for descending sort order.

We also need to make sure sorting order is deterministic. If multiple resources have the same value for the parameter we are sorting by, the resources need to be returned in the same order every time we execute the same query. We will use the `ResourceSurrogateId` column as the tie-breaker.

### Examples of requests and expected responses

Request | Response
--- | ---
GET <fhir-server>/Patient?_sort=-address-city | All patients (including patients whose address-city information is missing) sorted by address-city in descending order.
GET <fhir-server>/Patient?address-city=Ma&_sort=address-city | Only patients who have address-city starting with Ma and sorted in ascending order.
GET <fhir-server>/Patient?birthdate=lt1970-01-01&_sort=address-city | All patients who were born before 1970-01-01 and sorted by address-city in ascending order (including those Patients who don't have address-city information).

## Handling scenario where the sort parameter can contain multiple values

It is not easy to determine the cardinality of a parameter from the existing search paramter information. So our solution will have to be more generic compared to building something that specifically handles only parameters that have a cardinality > 1.

As an aside, knowing the cardinality of a parameter can be useful in other scenarions as well:

1. Knowing whether we need to apply the `DISTINCT` clause.
2. Knowing whether we can optimize `date:gt=x&date:lt=y` into `... WHERE DATE BETWEEN x AND y` instead instead of intersecting two sets `WHERE date > x` and `WHERE date < y`.

There are a couple of options we can look into for getting cardinality information:

1. John and Michael built something similar for the PowerBI query connector.
2. Firely library contains a class (`StructureDefinitionWalker`) that could be used as a starting point.
  

Irrespective of how many values a parameter has, only two of these values will be relevant for sorting. When sorting by ascending order, we will need to the "min" value and for descending we will need the "max" value. If we can keep track of these two values for each resource, we should be able to execute efficient SQL queries to select those values depending on whether we are sorting in ascending or descending order. During the search paramter extraction process (during creates and updates) we can add logic to determine which values are "min" and "max" for each parameter. We will add this information as part of the `ResourceWrapper` object and pass it to the datastore layer.

We will add two bit columns - `IsMin` and `IsMax` - to the `dbo.StringSearchParam` table. While inserting resources into the table, the appropriate SQL row generator will fill in the `IsMin` and `IsMax` values based on the values in the `ResourceWrapper` object. For parameters that have only one value, both `IsMin` and `IsMax` will be set to `1`. For parameters that have multiple values, we will have a combination of `1-0`, `0-1` and `0-0` depending on the values. Since these are bit columns the storage and performance cost is minimal. While querying for these resources, we will use the `IsMin` or `IsMax` column to filter values based on whether it is an ascending or descending sort.

### DateTime search values

DateTime search parameter are ranges by definition and hence we have two values for each parameter - `Start` and `End`. So when comparing datetime values we also have to choose which of the values to compare. When determining the max amongst two DateTime search values, we need to compare their `End` values and vice-versa for min.

### Reindex existing resources

To allow existing databases to be upgraded to this new schema, we will have to reindex existing resources so that they have the correct values for the `IsMin` and `IsMax` columns. We have an existing `$reindex` mechanism ([documentation here](./CustomSearch/Reindexing.md)) that can be used to trigger reindexing for the affected resources. More details on how we can use this is mentioned below.

## Handling scenario where sort parameter is not a required parameter for that resource

This is where things get a little tricky. Let's say we are sorting Patients by `address-city`. It is not enough if we search the `dbo.SearchStringParam` table to find `Patient` resources that have `address-city` values. We also need to find `Patient` resources that _do not_ have `address-city` values i.e. are missing in the `dbo.SearchStringParam` table. The challenge is to find this information in an efficient way.

### Option A

One option is to kind of "fill in the missing blanks". We can insert empty rows with default values for parameters for which the resource does not have an actual value. Since we don't want to add extra rows for every missing parameter for every resource, we will allow customers to decide which parameters they want to enable for `_sort`. We can add a configuration in appsettings that can store a list of parameters to support for `_sort`. We can add logic to the search extraction component to add "empty" data for these parameters.

To allow customers to enable this (or change supported parameters) for existing data, we will use the existing `$reindex` mechanism. Current reindex mechanism looks for new `SearchParameter` resources that have been added and then reindexes the affected resources (to learn more about how reindex works you can refer to the [documentation here](./CustomSearch/Reindexing.md)). We will use similar logic here except that the parameter name(s) will be specified in the request.

Since `_sort` is now enabled per parameter, we will need to keep track of which search parameters are supported for `_sort`. Once reindexing resources is complete, we will add a new document in the datastore to mark that the specified search parameter(s) is enabled for sort. This is building on top of the `SearchParameterRegistry` concept ([relevant documentation](./CustomSearch/SearchParameterRegistry.md)) and the `__searchparameterstatus__` documents we currently have. We'll build a similar registry (maybe named `SortParameterRegistry`) for parameters for which sort is enabled and this will act as the source of truth.

Pros:  

1. Simplifies our _sort queries.

Cons:  

1. We will be storing extra "empty" data which could cause cost and performance issues if it becomes big enough.
2. From customers' perspective they need to take extra steps before using `_sort`.
3. Additional logic to keep track and reindex supported search parameters.

### Option B

The plan is to split the query into two searches. One query will look for resources that satisfy the search criteria but do not have values for the parameter by which we are sorting. The other query will search for resources that have values for the sort parameter. The order of these queries will be determined by the sort order (ascending or descending).

Let's take an example query - `GET <fhir-server>/Patient?birthdate=lt1970-01-01&_sort=address-city`. The first query will look for all Patients whose birthdate is before 1970-01-01 and who _do not_ have address-city information. Once we finish returning all these results, we will start looking for Patients whose birthdate is before 1970-01-01 and who have address-city information and return them in ascending order by address-city. The order of the queries will be reversed if we want results in descending order.

We also have to consider that we might get 0 results for one of the queries. We have to gracefully handle that scenario since we would like to avoid returning an empty page of results. This sounds straightforward when we are sorting by one parameter (since we will have only 2 queries to run) but gets complicated once we support sorting by multiple parameters.

#### Continuation Token

Currently the ct encodes the last ResourceSurrogateId value that was seen so that subsequent queries can retrieve resources that come after that. Since we are planning to run multiple queries for sort we will also need to keep track of which query we are currently executing. The plan is to encode this information in the continuation token by keep track of the value for the sort parameter.  


Query | Continuation Token format
--- | ---
Querying for Patient resources that do not have address-city information | null-ResourceSurrogateId
Querying for Patient resources that have address-city information | (address-city)value-ResourceSurrogateId
   

__Note:__ For string search parameters, since `Text` can be large we have to limit the amount of data we encode in the continuation token. We will trim it appropriately before adding it to the continuation token.
  

This mechanim can similarly be extended to sort queries using multiple parameters. We can build a tree like structure that we can iterate over to figure out which query to execute next. The current format of the ct coupled with the sorting order for each parameter can be used to determine the next query to execute.  


param1 | param2 | Ct format
--- | --- | ---
null | null | null-null-ResourceSurrogateId
null | value | null-value-ResourceSurrogateId
value | null | value-null-ResourceSurrogateId
value | value | value-value-ResourceSurrogateId

Pros:

1. Things work as is - no extra data or steps required to support or enable `_sort.` 
2. More flexibility to optimize queries.

Cons:

1. The SQL query generation could become complex, especially when considering sorting by multiple parameters.

We will go with Option B.

## Supporting different locales

The collation rules (which affect the way strings are compared/sorted) can be different for different locales. Currently we use `Latin1_General` variants as default. There are two places that will get affected if we want to support different collation rules. The SQL Server database needs to know about this in order to run `ORDER BY` queries correctly. The fhir code also needs to know about this since we mark string search parameter values according to how they are sorted.

1. We will add a new setting to appsettings (something like `SqlServer:Locale`) that determines which locale the user wants to use. We will do some validation on this to make sure we support this. For PaaS, we will have to pipe this information via ARM to the Fhir application.
2. An updated schema where all the columns that currently have `COLLATE` set will be changed to `COLLATE $UserSetLocale$`.
3. When we read the script in order to build the schema, we will replace `$UserSetLocale$` with the correct collation rule depending on the locale set by the user. This mapping will be present in the `SchemaUpgradeRunner` (and related classes).
4. Similarly, during search paramter extraction we will use the `SqlServer:Locale` to set the current culture to correctly compare strings.


# Test Strategy

1. Unit tests and integration tests.
2. E2E tests for validating that search using `_sort` works as expected.

## Performance testing

We will do a basic amount of manual testing to make sure performance is within expected levels. There is a separate story for performance analysis of our overall API/database. Detailed performance testing and analysis will be part of that document.

# Security

N/A

# Other

## Summary of required changes

1. Schema update to keep track of multiple values for a single parameter.
2. New search value extraction logic on create/update resource operations (to track min and max values for a parameter).
3. Update logic to generate SQL query for searches that include `_sort`.
4. Additional logic to support "splitting" of `_sort` queries (based on whether we need to return resources that have missing values for the `_sort` parameter).
5. Additional logic in SqlServerSearchService to run multiple queries (in case there are 0 results for a query) before returning results.
6. E2E tests
