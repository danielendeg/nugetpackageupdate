Customers have been asking for functionality, such as [transactions](https://www.hl7.org/fhir/http.html#transaction), [chaining](https://www.hl7.org/fhir/search.html#chaining), and [reverse chaining](https://www.hl7.org/fhir/search.html#has), that really require a relational database in the backend. Cosmos DB's transactions are limited to a single partition (in our case, a single resource and its history) and it can't do joins across documents. It also has a number of limitations that could become problems: it does not let you fine-tune indexes, it does not support ordering by an arbitrary set of fields, and it is very much a black box (and a relatively immature one at that compared with SQL).

The plan is to offer SQL Server/Azure SQL as data provider option without necessarily deprecating Cosmos DB. In this document, we cover requirements and high-level design for adding this to the open-source FHIR server, while not covering the changes necessary to offer this in the managed PaaS offering. That will be a separate document.

[[_TOC_]]

# Requirements and Goals

- Read and write FHIR resources to a SQL database.
- Read and write control-plane (RBAC) data to a SQL database. We would prefer not to make administrators pay for and manage Cosmos DB and SQL. Control-plane data could be in a dedicated database or perhaps in its own schema in the FHIR database. **To be discussed**.
- Support Azure SQL, on-prem SQL Server, [localdb](https://docs.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-2016-express-localdb?view=sql-server-2017), cross-platform local development via Docker.
- Built-in support for safe schema migrations without downtime by way of a command-line tool for administrators. This tool and the FHIR server will be strict in preventing administrators from running incompatible code and schema versions.
- Good performance and scalability:
  - Data must be stored in a way such that FHIR searches are efficient. This is of course for good search performance but also because RBAC enforcement will be based on search expressions.
  - Operations that we think will be common must be fast. Optimize for insert over update. Optimize for reading the most recent version of a document over viewing its history.
  - Searches within a single compartment (either by a compartment search like `Patient/23/Observation` or by restricting to a single compartment like `Observation?subject=Patient/23`) should have sub-second latency regardless of database size. Population-based queries can take longer.
  - Keep chattiness to a minimum
- Correct and predictable behavior during concurrent reads and writes.
- Support [managed identities for Azure resources](https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/)
- Support everything the Cosmos DB provider does. These operations are:
  - **read**: Read the current state of the resource
  - **vread**: Read the state of a specific version of the resource
  - **create**: Create a new resource with a server assigned id
  - **update**: Update an existing resource by its id (or create it if it is new)
  - **delete**: Mark a resource as deleted
  - **hard delete**: Permanently delete all versions of a resource
  - **history**: Retrieve the change history for a particular resource, resource type, or all resources
  - **search**: Search the resource type based on some filter criteria
- Support **chained search**. For example, `/DiagnosticReport?subject.name=peter`: give me all the DiagnosticReports for subjects named "peter", or `DiagnosticReport?subject:Patient.name=peter`: Give me all the DiagnosticReports for *patients* named "peter".
- Support **reverse-chained search**. For example: `/Patient?_has:Observation:patient:code=1234-5`: give me all Patient resources where the patient resource is referred to by at least one Observation with code of 1234, and where the Observation refers to the patient resource though the patient search parameter.
- Support **`_include`**. For example: `MedicationRequest?_include=MedicationRequest:patient`, which returns all MedicationRequests and includes the patient resources that the medication requests refer to.
- Support **`_revinclude`**. For example: `MedicationRequest?_revinclude=Provenance:target`, which returns all MedicationRequests and includes and Provenance resources that refer to them.
- Lay the foundation for implementing operations we cannot currently support:
  - **batch/transaction**: Update, create or delete a set of resources in a single interaction
- Possibly support specifying sort order in a search, for example `Observation?_sort=status,-date,category`? (See [Search Sorting](#search-sorting))

We need to acknowledge that it will be impossible to deliver a one-size-fits-all implementation that will perform optimally for all workloads. We're pretty sure that the schema and index design will evolve as we get feedback from customers. We may even publish different index "packages" for different workloads.

See [Related Features](#related-features) for features that are related but outside the scope of this spec.

See [Development Plan](#development-plan) for a proposal on how we stage the delivery of this feature.

# High-level Design

## Problem Domain

First, we should cover the problem domain of fhir resources, types, and search parameters. This will inform our choice of schema.

We will focus on the FHIR data plane database here. The control plane database will be a lot simpler.

- We will be storing **FHIR resources**. These can be represented as either XML or JSON.
- Each FHIR resource is of a **resource type**, such as `Patient`, `Practitioner` or `Observation`. There are around 150 of these, but we may eventually support custom types as well.
- Each resource can have many **versions**. When searching, only the most recent version is considered.
- Each resource type defines a number of **search parameters**. Search parameters have one of the following types:
  - Number
  - Date/DateTime
  - String
  - Token
  - Reference
  - Composite
  - Quantity
  - URI
  - Special
- For a resource, each search parameter can have 0 or more **search parameter values**.

## Schema

We're not going to specify the exact schema and indexes in this document, because we will be comparing number of approaches over the course of implementation. The "best" approach will be chosen by considering ingestion and search performance for various database sizes (10k, 100k, hopefully 1M patients). But we will likely have something that resembles the following schema.

### Tables

#### `ResourceType`

- Used to assign a `smallint` ID for each resource type.
- Populated by the server on startup.
- Fields:
  - **`ResourceTypeId`**
  - `Name`
  - `Uri` (e.g. "http://hl7.org/fhir/StructureDefinition/Patient")

#### `SearchParameter`

- Used to assign a `smallint` ID for each search parameter definition (or component of a composite search parameter definition).
- Populated by the server on startup.
- Fields:
  - **`SearchParameterId`**
  - `Name` (the simple name, e.g. "Patient-name")
  - `Uri` (e.g. "http://hl7.org/fhir/SearchParameter/Patient-name")
  - `ComponentIndex` (populated if the parameter is a component of a composite parameter)

#### `Compartment`

- Used to assign a `tinyint` ID for each compartment type.
- Populated by the server on startup.
- Fields:
  - **`CompartmentId`**
  - `Name`

#### `Resource`

- Used to store the raw resource contents, along with its version number and creation timestamp.
- The raw resource will be compressed on the FHIR server in order to save space (5x on average with Synthea data). If we ever want to operate on the JSON data from within T-SQL, we would need to use the `DECOMPRESS` function on the resource data first.

- Fields:
  - **`ResourceSurrogateId`**
  - `ResourceTypeId`
  - `ResourceId`
  - `Version`
  - `Timestamp`
  - `RawResource`

#### `ResourceModificationLog`

- Used to store configurable claims about the principal that created or updated a resource version
- Fields:
  - `ResourceSurrogateId`
  - `ClaimType`
  - `ClaimValue`

#### `DateSearchParam`

- Stores date/datetime search parameter values
- Fields:
  - `ResourceTypeId`
  - `ResourceSurrogateId`
  - `SearchParamId`
  - `IsHistory`
  - *`DateFrom`*
  - *`DateTo`*

#### `NumberSearchParam`

- Stores number search parameter values
- Fields:
  - `ResourceTypeId`
  - `ResourceSurrogateId`
  - `SearchParamId`
  - `IsHistory`
  - *`NumberFrom`*
  - *`NumberTo`*

#### `QuantitySearchParam`

- Stores quantity search parameter values
- Fields:
  - `ResourceTypeId`
  - `ResourceSurrogateId`
  - `SearchParamId`
  - `IsHistory`
  - *`QuantityCodeId`*
  - *`QuantityFrom`*
  - *`QuantityTo`*

#### `ReferenceSearchParam`

- Stores reference search parameter values
- Fields:
  - `ResourceTypeId`
  - `ResourceSurrogateId`
  - `SearchParamId`
  - `IsHistory`
  - *`ReferenceResourceTypeId`*
  - *`ReferenceResourceId`*
  - *`ReferenceVersion`*

#### `StringSearchParam`

- Stores sting search parameter values
- Fields:
  - `ResourceTypeId`
  - `ResourceSurrogateId`
  - `SearchParamId`
  - `IsHistory`
  - *`Value`*

#### `TokenSearchParam`

- Stores token search parameter values, without the text field
- Fields:
  - `ResourceTypeId`
  - `ResourceSurrogateId`
  - `SearchParamId`
  - `IsHistory`
  - *`SystemId`*
  - *`Code`*

#### `TokenTextSearchParam`

- Stores the text field of token search parameter values
- Fields:
  - `ResourceTypeId`
  - `ResourceSurrogateId`
  - `SearchParamId`
  - `IsHistory`
  - *`Text`*

(#token-text) table.

#### `UriSearchParam`

- Stores URI search parameter values
- Fields:
  - `ResourceTypeId`
  - `ResourceSurrogateId`
  - `SearchParamId`
  - `IsHistory`
  - *`Uri`*

#### `CompartmentAssignment`

- Stores the compartments that a resource is part of
- Fields:
  - `ResourceTypeId`
  - `ResourceSurrogateId`
  - `IsHistory`
  - *`CompartmentId`*
  - *`ReferenceResourceId`*

#### `System`

- Stores the unique systems referenced in `TokenSearchParam.System` and `QuantityCode.SystemId`.
- Fields:
  - `SystemId`
  - *`System`*

#### `QuantityCode`

- Stores the unique codes referenced in `QuantitySearchParam.QuantityCodeId`.
- Fields:
  - `QuantityCodeId`
  - *`SystemId`*
  - *`Code`*

### Indexes

The search parameter tables will generally have the following indexes:

- Clustered Index:
  - ResourceSurrogateId
  - SearchParameterId
  - [Type-specific fields, such as Code, System, Quantity, etc.]
- Non-clustered Index (Filtered on IsHistory=0)
  - [Type-specific fields, such as Code, System, Quantity, etc.]
  - ResourceTypeId

This index design will allow for inner-loop joins for single-compartment or single-resource referenced searches, such as "find all weight observations for patient x where the weight was greater than 100 kg". The query plan for this kind of query will be a non-clustered index range scan on `ReferenceSearchParam` with an inner loop join using the using a clustered index seek.

### Page Compression

We will be using page compression on most parameter tables and indexes where we expect a lot of duplicated strings.

### Duplicated Strings

Many codes, systems, and URIs are duplicated across FHIR resources. Some examples from a database with 100k Synthea patients:

- There are only 42 distinct systems
- There are only 34 distinct codes in the `QuantitySearchParam` table
- There are around 800 distinct token text strings.
- The top 20 URIs in the `UriSearchParam` table are reused extensively, but there is a very long tail.

We will adopt the strategy of using integer identifiers for systems and quantity codes and storing them in-memory in the FHIR server. Other strings, such as those in TokenText, we will rely on page compression. The reason for this is that insertion logic gets more expensive and can cause more blocking if it needs to upsert into other tables.

### Composite Search Parameters

[Composite](https://www.hl7.org/fhir/search.html#composite) search parameters require special handling. They are search parameters that are made up of other search parameters, and, like all search parameters, they can have multiple values for a single resource. For example, `Observation?component-code-value-quantity=http://loinc.org|8480-6$lt60` searches for observations where the systolic reading is less than 60. In this example, there are two components to the composite parameter: the code of the reading and the quantity.

There are two obvious approaches to handling these parameters. The first is to include a `CompositeInstanceId` column on each search parameter table and include that field in the join criteria. The value is unique for each value of a composite search parameter per resource. The query would need to look something like this:

``` SQL
SELECT RawResource FROM Resource r
WHERE r.ResourceTypeId = @p0
      AND EXISTS(
          SELECT *
          FROM dbo.TokenSearchParam token
          WHERE token.ResourceSurrogateId = r.ResourceSurrogateId
                AND token.SearchParamId = @p1
                AND token.System = @p2
                AND token.Code = @p3
                AND EXISTS(
                    SELECT *
                    FROM dbo.QuantitySearchParam quantity
                    WHERE quantity.ResourceSurrogateId = r.ResourceSurrogateId
                          AND quantity.SearchParamId = @p4
                          AND quantity.Quantity < @p5
                          AND quantity.CompositeInstanceId = token.CompositeInstanceId
                )
      )
```

The other approach would be to generate a table for each combination of parameter types in composite across all search parameters, in effect denormalizing the composite search parameter storage. In the base STU3 model, this would require tables for:

- Reference$Token
- Token$Token
- Token$Date
- Token$Quantity
- Token$String
- Token\$Number\$Number.

The query for the same search as before would now look something like this:

``` SQL
SELECT RawResource FROM Resource r
WHERE r.ResourceTypeId = @p0
      AND EXISTS(
          SELECT *
          FROM QuantityTokenCompositeSearchParameter composite
          WHERE composite.SearchParamId = @p1
                AND composite.ResourceSurrogateId = r.ResourceSurrogateId
                AND composite.Token1System = @p2
                AND composite.Token1Code = @p3
                AND composite.Quantity2Quantity = @p5
      )
```

As you can see, this version has only one `EXISTS` subquery, so we would expect it to perform better. The main drawback of this approach is the additional complexity of managing these extra tables (and therefore versioning them would not be done in just static T-SQL).

The decision is to go with the second option, because searches on composite parameters are very useful and common. We will not be generating these tables dynamically, but instead will create tables for combinations that are defined in STU3 and R4.

### String Column Lengths

The maximum index sizes allowed by SQL Server are 900 bytes and 1700 bytes for clustered and non-clustered respectively. The `varchar` datatype requires 1 byte per character, whereas the `nvarchar` requires 2. Here are proposed types and limits for the search parameter columns:

| Column                         | Data Type       |
|--------------------------------|-----------------|
| `TokenSearchParameter.Code`    | `varchar(128)`  |
| `StringSearchParam.Value`      | `nvarchar(512)` |
| `QuantitySearchParameter.Code` | `varchar(128)`  |
| `TokenTextSearchParam.Text`    | `nvarchar(434)` |
| `Uri.Uri`                      | `varchar(512)`  |

As mentioned later, varchars SQL Server 2019 can optionally be UTF8-encoded, in case codes and URIs have non-latin characters.

## Collations

Search on string parameters is by default case- and accent-insensitive, and matches strings that start with the predicate. For example, `Patient?=given=fred` should match "Frédéric". The `:exact` specifies an exact match. `Patient?=given:exact=Fred` would only match the name "Fred". The will specify an accent- and case-insensitive collation for the `TokenText.Text` and `StringSearchParam.Value` columns. Queries can specify an accent and case-sensitive collation when necessary.

The topic of collations is complex. To give an example, in Danish, the correct way of removing diacritics from the string "ål" would be "aal", and an the string "aa" would be sorted last in the list "a", "b", "c", "aa", because å is the last letter of the Danish alphabet.

One could argue that a FHIR server deployed in Denmark should use a Danish collation. We could consider making it a configuration option.

Separately, UTF8 storage is now an option in SQL Server 2019 (in preview at time of writing). With a UTF8 collation, varchar columns are stored in UTF8, which should be great for codes and URIs that are not expected to contain non-ascii characters but could.

## Search Sorting

We don't currently support sorting with the Cosmos DB backend. Per the spec, sort order is specified with a sequence of search parameters, each ascending or descending. The challenge with sort is that each search parameter can have multiple values. The spec says that the value that should be considered when sorting is the one that would come first. This might be implemented with a bit column on each of the search parameter tables indicating whether the row represents a resource's sorting value for a given search parameter. Our Entity Attribute Value-like schema will probably not perform well in this case.

Vonk only supports sorting by _lastUpdated. HAPI seems to fully support the feature, but, to take an example, [ordering Observations by code](http://hapi.fhir.org/baseDstu3/Observation?_sort=code) takes 15 seconds.

If we are serious about supporting this, an option that might perform better would be to  generate a table per resource with a column per search parameter. This will make things a lot more complex though.

## Maintaining History

To maintain history, we have some choices. We can maintain all versions of a resource in the `Resource` table. This has the advantage of simplicity and the table being append-only (except for the hard delete case). When searching for the most recent version of resources, you can do something like this:

``` SQL
SELECT *
FROM RESOURCE r
WHERE r.ResourceTypeId = @p0
      AND NOT EXISTS (
        SELECT *
        FROM Resource r2
        WHERE r.ResourceSurrogateId = r2.ResourceSurrogateId
        AND r2.Version > r.Version
      )
```

Another approach would be to have a `ResourceHistory` table and when a resource is updated, we copy the current version to the history table. This would make updates more expensive (and a bit trickier from a concurrency perspective), but would improve read performance.

We can do something similar with the search parameter tables. We either leave them as-is or archive entries to history tables on updates. Note that we need to maintain search parameters for enforcing RBAC on history data. We also need this to support chained searches where references are to a specific FHIR resource's version.

### Using Temporal Tables

SQL Server also has a [Temporal Tables](https://docs.microsoft.com/en-us/sql/relational-databases/tables/temporal-tables?view=sql-server-2017) feature, where the system automatically maintains the history of a table's state. For a table with this feature enabled, the system maintains a history table. Both the main table and history table have start and end time columns that are system-managed. When you update a row in the main table, the system automatically copies existing row to the history table (updating its end time value) before updating the row in the main table. You can then query the main or history table directory, or you can query the main table with the `FOR SYSTEM_TIME` clause giving you a view of the data at a point in time or over a time range. It's a nice feature.

The main drawback will be supporting hard delete. Directly deleting from the history table is not supported. Do do this, you need to turn off the system version feature on the table altogether. This can be done in a transaction:

``` SQL
BEGIN TRAN

-- Repeat for Resource and each search parameter table:

ALTER TABLE MyTable SET (SYSTEM_VERSIONING = OFF);

-- Perform hard delete here...

-- Now restore versioning.
ALTER TABLE MyTable SET
(
    SYSTEM_VERSIONING = ON (HISTORY_TABLE = MyTableHistory)
);
COMMIT;  
```

This is a bit messy and we would need to ensure that all other writes are blocked during this time. Also the server would need to have alter table permissions, which otherwise would not be needed.

### Decision

Because references between FHIR resources can specify a target resource version, we will keep historical search parameters in the same table. Nonclustered indexes on search parameter tables will be filtered, including on search parameters for the current version of a resource.

## Search Paging

There are two common ways of doing paging. One is by using positional offsets:

``` SQL
SELECT *
FROM X
ORDER BY Id
OFFSET ((@pageNum) * @pagesize) ROWS
FETCH NEXT @pageSize ROWS ONLY;
```

The other is "keyset pagination", where you use a WHERE clause based on the values of the ORDER by field values:

``` SQL
SELECT TOP (@pageSize) *
FROM X
WHERE Id > @lastId -- gets more complicated with more order by fields
ORDER BY Id ASC
```

Keyset pagination can perform better but it can also completely change the query plan for already complex queries, so its performance can be hard to predict. OFFSET/FETCH is vulnerable to duplicated or skipped results across pages as rows are inserted or deleted. FWIW, Vonk and HAPI use offset-based paging.

We will aim for keyset pagination, but may need to fall back to offset if performance is an issue.

**Open question**: if we support ordering by last updated time, you could see the same resource show up twice in different pages if it is updated while a client is enumerating a search feed. Is this acceptable? The other option is for all pages of a search to be based on a snapshot taken at at the first page. The SQL could look something like this:

``` SQL
DECLARE @maxResourceSurrogateId BIGINT = (SELECT MAX(ResourceSurrogateId) from dbo.Resource)

SELECT * FROM Resource r
WHERE NOT EXISTS (
      SELECT * FROM Resource r2
      WHERE r2.Id = r.Id
      AND ((r.ResourceSurrogateId <= @maxResourceSurrogateId AND r2.Version > r.Version) OR
           (r.ResourceSurrogateId > @maxResourceSurrogateId AND r2.Version < @maxResourceSurrogateId))
)
```

### Paging and Slow Queries

Some complex population-level queries can be slow to execute (many seconds or even minutes), and often the entire resultset needs to be computed by the database to retrieve a particular page. So it's not hard to create examples of queries that can take minutes to execute per page. A much more efficient approach would be to to snapshot the query result's resource surrogate IDs in tempDB and base the paged requests on this snapshot. Clients would have a certain amount of time to fetch the next page of data before the snapshot is discarded.

This approach is obviously more complex. We would need to have a recurring task that purges these result sets. We would need to be concerned about tempdb storage exhaustion. And we would need to he careful that RBAC roles of the principal have not changed in between requests.

## Total

R4 now has a `_total` search parameter, which allows the client to give a hint as to whether it requires a total count of resources. We should honor it, although it is optional. Possible values are:

| Value    | Meaning                                                                                         |
|----------|-------------------------------------------------------------------------------------------------|
| none     | There is no need to populate the total count; the client will not use it                        |
| estimate | A rough estimate of the number of matching resources is sufficient                              |
| accurate | The client requests that the server provide an exact total of the number of matching resources  |

## Insert

Inserting a resource involves writes to potentially all tables. We will be using [Table-Valued parameters](https://docs.microsoft.com/en-us/dotnet/framework/data/adonet/sql/table-valued-parameters) to pass many rows to a single stored procedure, thus avoiding many calls to the database for each resource. This stored procedure body may look something like this:

``` SQL
BEGIN TRANSACTION

DECLARE @resourceSurrogateId bigint
DECLARE @version int = SELECT MAX(Version) FROM dbo.Resource WHERE ResourceTypeId = @resourceTypeId AND Id = @id) + 1

IF @version IS NULL
    SET @version = 1

INSERT INTO dbo.Resource
(ResourceTypeId, Id, Version, LastUpdated, RawResource)
VALUES (@resourceTypeId, @id, @version, SYSUTCDATETIME(), @rawResource)
SET @resourceSurrogateId = SCOPE_IDENTITY();

INSERT INTO dbo.StringSearchParam
(ResourceTypeId, ResourceSurrogateId, SearchParamId, CompositeInstanceId, Value)
SELECT ResourceTypeId, @resourceSurrogateId, SearchParamId, CompositeInstanceId, Value FROM @tvpStringSearchParam

INSERT INTO dbo.TokenText (Hash, Text)
SELECT Hash, Text
FROM @tvpTokenText p
WHERE NOT EXISTS (SELECT 1 FROM dbo.TokenText where [Hash] = p.Hash)

INSERT INTO dbo.TokenSearchParam
(ResourceTypeId, ResourceSurrogateId, SearchParamId, CompositeInstanceId, System, Code, TextHash)
SELECT ResourceTypeId, @resourceSurrogateId, SearchParamId, CompositeInstanceId, System, Code, TextHash FROM @tvpTokenSearchParam

INSERT INTO dbo.DateSearchParam
(ResourceTypeId, ResourceSurrogateId, SearchParamId, CompositeInstanceId, StartTime, EndTime)
SELECT ResourceTypeId, @resourceSurrogateId, SearchParamId, CompositeInstanceId, StartTime, EndTime FROM @tvpDateSearchParam

DECLARE @dummy int
DECLARE @uriMerged TABLE ([UriId] int, [Uri] varchar(512))

MERGE dbo.Uri AS t
USING @tvpUri AS s
ON t.[Uri] = s.[Uri]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Uri]) VALUES ([Uri])
WHEN MATCHED THEN
    UPDATE SET @dummy = 1
OUTPUT inserted.* INTO @uriMerged;

INSERT INTO dbo.ReferenceSearchParam
(ResourceTypeId, ResourceSurrogateId, SearchParamId, CompositeInstanceId, BaseUriId, ReferenceResourceTypeId, ReferenceResourceSurrogateId)
SELECT p.ResourceTypeId, @resourceSurrogateId, p.SearchParamId, p.CompositeInstanceId, m.UriId, p.ReferenceResourceTypeId, p.ReferenceResourceSurrogateId
FROM @tvpReferenceSearchParam p
LEFT JOIN @uriMerged m
ON p.[BaseUri] = m.[Uri]

INSERT INTO dbo.QuantitySearchParam
(ResourceTypeId, ResourceSurrogateId, SearchParamId, CompositeInstanceId, System, Code, Quantity)
SELECT ResourceTypeId, @resourceSurrogateId, SearchParamId, CompositeInstanceId, System, Code, Quantity FROM @tvpQuantitySearchParam

INSERT INTO dbo.NumberSearchParam
(ResourceTypeId, ResourceSurrogateId, SearchParamId, CompositeInstanceId, Number)
SELECT ResourceTypeId, @resourceSurrogateId, SearchParamId, CompositeInstanceId, Number FROM @tvpNumberSearchParam

INSERT INTO dbo.UriSearchParam
(ResourceTypeId, ResourceSurrogateId, SearchParamId, CompositeInstanceId, Uri)
SELECT ResourceTypeId, @resourceSurrogateId, SearchParamId, CompositeInstanceId, Uri FROM @tvpUriSearchParam

COMMIT TRANSACTION

SELECT @version
```

We will eventually generalize this stored procedure to be able accept many resources at a time, which will make bulk insert more efficient.

## Concurrency

Choosing the right isolation level and locking strategy for our different scenarios will be an important consideration.

### Searches during Insertions

An insertion will be done in an a transaction. When a search is performed at the same time as an insertion, we do not want the search to be blocked by the transaction, and we do not want the search to "see" the partially-written or "dirty" search parameter rows, as they could incorrectly include the new resource in the search result (for instance in the case of `?name:not=john` but the "john" search parameter value has not been written yet).

For this reason, we will enable [Read Committed Snapshot Isolation (RCSI)](https://docs.microsoft.com/en-us/dotnet/framework/data/adonet/sql/snapshot-isolation-in-sql-server) on the database. This means that, when a statement uses `READ COMMITTED` isolation, it will see data as it was when the statement began. We will measure the performance cost, but it should be small because we don't currently expect any `UPDATE` sql statements, only `INSERT` (mostly) and `DELETE`.

### Searches during Updates

During updates, we run the risk of both versions of a resource appearing in a search result. RCSI will be our solution here too.

## Ingestion Performance

After doing some experiments, we found ingestion performance against a Premium tier 8-core Azure SQL instance to be capped at around 8k-10k patients per hour. The bottleneck is page latch contention because all concurrent writes are writing to the same pages because of the resource surrogate ID sequence. 

Eliminating the surrogate ID was about 2x slower. Using a pool of sequences improves performance somewhat, but it was not a major breakthough.

Modifying the insert stored procedure to process a patient bundle at a time improved performace to 48k patients per hour.

### In-Memory Tables

To improve the performance of single-resource writes though the HTTP endpoint, particularly loads that are very spiky, we can consider the ["shock absorber" or "landing pad"](https://cloudblogs.microsoft.com/sqlserver/2013/09/19/in-memory-oltp-common-design-pattern-high-data-input-rateshock-absorber/) design pattern. This pattern uses in-memory tables to ingest data, and periodically moves the rows to disk-based tables.

### Transactions

While we will not be implementing fhir [transactions](https://www.hl7.org/fhir/http.html#transaction) right away, we will need to think about what the appropriate isolation level should be. Serializable is the most predictable, but throughput will suffer if many concurrent transactions read or write the same data.

## ORMs

We don't plan to use Entity Framework or Dapper for the FHIR data plane (though we could for the control plane). We want flexibility to craft the most efficient statements that we can and the schema of most (or all) queries will be just the raw resource, so deserializing rows to different kinds of objects will not be a concern.

## Schema migrations

Schema migrations are defined in the [Schema Migrations](Schema-Migrations.md) document.

# Test Strategy

We want to run all our existing E2E tests that currently target Cosmos DB against both Cosmos DB and SQL Server. We will also soon start supporting FHIR R4 in addition to STU3, and where possible we would like to reuse or generalize these tests as well. And we also currently run a subset of our tests in both XML and JSON serialization modes.

So we will need a structured way of specifying which configuration combinations we want to test against (or all of them), and have the tests run locally as well as in our CI environment. The CI environment will eventually deploy a STU3 SQL-backed instance, an R4 SQL-backed instance, a STU3 CosmosDB-backed instance, and a R4 CosmosDB-backed instance.

Also, some tests will only apply to certain configurations. For example, tests that verify search parameter chaining should not run against Cosmos DB, where the functionality is not supported.

We will develop an extension to [xUnit.net](https://github.com/xunit/xunit) that will allow parameterizing a test fixture's constructor. 

A test class can look something like this:

``` csharp
[FixtureVariants(FhirVersion.Stu3 | FhirVersion.R4, DataStore.Sql | DataStore.Cosmos)]
public class MyTests : IClassFixture<MyFixture>
{
    private readonly ITestOutputHelper _output;
    private readonly MyFixture _myFixture;

    public MyTests(ITestOutputHelper output, MyFixture myFixture)
    {
        _output = output;
        _myFixture = myFixture;
    }

    [FhirVariantFact]
    public void MyTest()
    {
        _output.WriteLine(_myFixture.DataStore.ToString());
        _output.WriteLine(_myFixture.FhirVersion.ToString());
    }

    [FhirVariantFact(dataStore: DataStore.Sql)]
    public void SqlOnlyTest()
    {
        _output.WriteLine(_myFixture.DataStore.ToString());
        _output.WriteLine(_myFixture.FhirVersion.ToString());
    }
}
```

The the class uses a fixture that can look like this:

``` csharp
public class MyFixture
{
    public DataStore DataStore { get; }
    public FhirVersion FhirVersion { get; }

    public MyFixture(FhirVersion fhirVersion, DataStore dataStore)
    {
        DataStore = dataStore;
        FhirVersion = fhirVersion;

        // Initialize based on data store and FHIR version...

    }
}
```

With a custom xUnit `XunitTestFramework` implementation, we can create synthetic test classes for the cartesian product or variant combinations, each of which will instantiate a fixture with one of the supported combinations of variants.

![xUnit tests in VS](images/xunit-tests.png)  

# Security

Security configuration will be a bigger topic for PaaS. For OSS, we will provide an ARM template for deploying a FHIR service and an Azure SQL database. The FHIR service will use [managed identities for Azure resources](https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/) to authenticate with the database. The FHIR service's service principal will be configured on the database server with the least privileges necessary to function. It should not require anything beyond reading and writing data (No need to create or drop tables, as that will be done by the admin tool).

# Related Capabilities

## Referential Integrity

A FHIR server can optionally validate and enforce referential integrity. This involves verifying, for example, that the patient that an observation resource references actually exists on the server.

Referential integrity checks would happen on writes (create/update/delete) and also during for a [`$validate`](https://www.hl7.org/fhir/resource-operation-validate.html) operation.

This can be done efficiently thanks to the extracted reference search parameters for each resource. We can use these on ingestion to verify that the targets exist and on deletion to verify that no other resources refer to the resource being deleted.

## Custom Search Parameters

We will eventually support customers defining their own search parameters beyond those in the base structure definition. Here is an example of a search parameter definition:

``` JSON
{
    "resourceType": "SearchParameter",
    "id": "Patient-language",
    "url": "http://hl7.org/fhir/SearchParameter/Patient-language",
    "name": "language",
    "status": "draft",
    "experimental": false,
    "date": "2017-04-19T07:44:43+10:00",
    "publisher": "Health Level Seven International (Patient Administration)",
    "contact": [
        {
            "telecom": [
                {
                    "system": "url",
                    "value": "http://hl7.org/fhir"
                }
            ]
        },
        {
            "telecom": [
                {
                    "system": "url",
                    "value": "http://www.hl7.org/Special/committees/pafm/index.cfm"
                }
            ]
        }
    ],
    "code": "language",
    "base": [
        "Patient"
    ],
    "type": "token",
    "description": "Language code (irrespective of use value)",
    "expression": "Patient.communication.language",
    "xpath": "f:Patient/f:communication/f:language",
    "xpathUsage": "normal"
}
```

The key fields are `name`, `type`, `base`, and `expression` (a FhirPath expression).

New resources added after a search parameter is added to a server will have the new parameter extracted and ready for searches, but existing data will not know anything about the new search parameter. For this, we will need to update the indexed search parameter values for each applicable resource.

While related to a schema migration, we will treat a re-indexing operation as something separate, one that is unrelated to a migration. (This holds as long as we are not generating a schema based on a system's search parameters). The reasons for this are:

1. Adding a new search parameter does not require changes to the schema
1. Re-indexing cannot easily be done purely in T-SQL because there is non-trivial C# logic in the FHIR server to perform the extraction and re-implementing it in T-SQL would be challenge (only Azure SQL Managed Instance supports CLR functions). Therefore, one or more worker or web processes will need to pull down the existing resources, run search parameter extraction, and update the database with the results.

This process should probably be initiated and managed from the control plane.

## GraphQL

[GraphQL](https://graphql.org/) is a query language created by Facebook. There is a draft [proposal](https://hl7.org/fhir/2018Jan/graphql.html) supporting GraphQL on a FHIR server. The query language allows shaping a result set and including related resources in a single call. The latter part can be efficiently implemented on top of the SQL data provider, similar to `_include` and `_revinclude`.

## SQL on FHIR

[SQL on FHIR](https://github.com/FHIR/sql-on-fhir) is a proposed specification for querying FHIR data directly with ANSI SQL. An example from their repo is:

``` SQL
SELECT subject.reference,
       AVG(value.quantity.value) avg_hdl
FROM observation o,
     UNNEST(o.code.coding) c
WHERE c.system = 'http://loinc.org' AND
      c.code = '2085-9' AND
      o.effective.datetime > '2017'
GROUP BY subject.reference
```

SQL on FHIR is entirely separate from FHIR search capabilities. FHIR search is based on extracted search parameter values, whereas SQL on FHIR preserves all arrays and nested structures of the resources.

While SQL on FHIR is a really interesting idea, this work we are doing here will not support it. In theory, you could build FHIR search on top of SQL on FHIR. But in order to have good search performance, you would probably still want to eagerly extract the search parameters to index them.

What we're designing here is a backend to support a FHIR service's core capabilities: CRUD and search. These could be viewed as OLTP scenarios, whereas SQL on FHIR could be seen as more useful in analytical scenarios. So we may support SQL on FHIR one day, but it does not necessarily build on what we have here, nor does it need to be offered from the same database.

Note that SQL on FHIR will not work out of the box on SQL Server because it depends on array columns and structs. So when/if we offer support it, our best bet will probably be PostreSQL.

# Development Plan

Some work can easily be done in parallel:

1. Core FHIR data plane implementation
1. Test infrastructure: running the tests against different configuration combinations (Cosmos, SQL) X (STU3, R4) X (JSON, XML)
1. Database schema migration tool.
1. Control plane data layer implementation.

The Core FHIR data layer implementation will be the long pole here. Work will be staged in this way:

1. Build out schema + insert stored procedure.
1. Load up 10k or 100k patient.
1. Experiment with several schema, query, and index variants.
1. Lock on the schema based on results from the previous experiments.
1. Deliver parity with Cosmos DB. Make all tests pass.
1. Add support for chaining
1. Add support for reverse changing
1. Add support for `_include`
1. Add support for `_revinclude`
1. Add support for `_sort`
1. Begin work in bundles and transactions.
