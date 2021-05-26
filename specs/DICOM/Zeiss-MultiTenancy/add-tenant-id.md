# Add Tenant ID

This discusses the adding of a first class tenant ID to dicom.

## Dicom Consistency

This means that it will not be possible to construct a query, get or store that will violate the standard. We will guarantee that all study-series-instance ids are partitioned via the tenant ID.

This means it will be possible to allow multiple conflicting study-series-instance ids in differenat tenants on the same server.

## High level Public behavior

### Add tenant to all APIs

We will add the ability to specify a tenant to all Apis. 

#### How specified
Options: (can do both)
* Header
    * URL's stay the same
        * Don't have to worry about url specific behavior in dicom
            * Need to research if this is necessary
    * Requires specific knowledge of client to know about this nonstandard behavior
    * Using header added information is consistent with future extensions
        * since we may add features in the future we don't have to worry about ordering in the URL path
* URL
    * need to ensure that we don't break dicom standard
    * Allows client app to tenant isolated endpoint
        * We need to understand security & intended audience for messaging.
            * It is common for proxies to add header information and add security guarantees. With URLs we need to ensure we have appropriate security policies in place to allow tenants to not alter eachother.

#### Format of ID
Options: 
1. Integer
    1. This would allow us to not need to lookup the identifier on queries.
1. Some string long enough to hold a UUID
    1. Requires a redirection on lookup potentially.

#### Default value & up-converting existing subscriptions

I think it will be simplest to have the default value be null.
* We would need to ensure that string IDs didn't also allow a value of "" (empty & null is confusing) and we should treat them the same

Another idea is to have a logical default value "" for string, and 0 for integers
* That things like dicom cast change feed will start to always have the value.
* It will be a little bit harder to do the database migration

To up-convert an existing subscription, we will the default value as a column to the appropriate tables.

To allow a user to migrate the data to a different tenant we tell them to re-upload with the added tenant ID. 

### Cross tenant queries

The goal of this proposal is not to solve cross tenant queries. 

Currently there is no information associated with a tenant. We may choose to extend this later. It could be a path associated with the tenant & or a set of tags. This potentially allows searches & queries to be described with nested tenancy or across non nested items applied to a whole tenant.

### Requirements / Burden placed on user

There are no default requirements placed on a user that does not wish to use multitenancy. It requires those that wish to operate in tenant boundaries to add a tenant for every api they call.

We currently do not require a user to precreate a tenant before they use it. They are automatically created / recreated any time a stow occurs.


### New Apis required

We do not need a create for tenant because they are implicitly created (and deleted).

We do need a list / enumerate of tenants. This would allow the ability to enumerate / delete all data.


## Dicom Cast integration

Dicom cast integration is out of scope. We will include the tenant ID in the dicom change feed the value is not null.


## Detailed implementation

### Persisted information

#### Databases

1. new table for tenants.
    1. Currently this will have noinformation other than the ID.
1. Add tenant id foreign key to all tables
    1. Instance
    1. Study
    1. Series
    1. DeletedInstance
1. Add tenant id to the following tables (not as a foreign key)
    1. ChangeFeed
The foreign key constraint will `ON DELETE RESTRICT`

#### BlobStore

I seem to recall the format of the blob & metadata store(also implied by the format of the soft delete table). Contains instance, study, series format. Which means we will have to prefix/suffix it with tenant information to differentiate it from other formats.

For default tenant we would not need to prefix/suffix blob & metadata store.

### Tenant management

#### Creation

Creation is implicit in store.
Options:
1. Stored procedure.
    1. Have a stored procedure that checks if the tenant exists, if not ads it & then inserts the existing records.
1. Insert record assuming tenant id exists
    1. Catch failure
    1. Create the tenant
    1. Retry initial insert

#### Deletion

It is useful to eventually garbage collection tenants. Periodically we will run a sweeper thread that will check tenants that have zero reliances on them. Then we will delete those tenants.

 ## Pros and Cons

- ✔️ All data stays consistent in the system.
- ✔️ Allows simple client logic.
- ✔️ Serves as a potential basis to add tenant related default tags / nested tenancy.
- ❌ Does not solve cross tenant query problems.