We need to add support for SQL in PaaS because both FHIR and DICOM are ready to use it. This document describes the infrastructure changes needed to support SQL.

[[_TOC_]]

# Business Justification

FHIR already supports SQL as the persisted storage provider in the OSS code base. It provides additional sets of capabilities, such as Transaction, that are difficult to implement using Cosmos DB. DICOM at the moment only supports SQL as the persisted storage provider in the OSS code base.

To allow these new functionalities, we need to bring SQL to the managed service, which requires new sets of infrastructure, operational procedures, monitoring, and so forth.

In this document, we will focus on the phase 1 of the infrastructure changes that will be needed. Service specific changes will be addressed in different specs.

## MVP Plan

The timeline we are looking at is:

Q2 (Dec 2020) - DICOM/FHIR private preview with consumption model.
Q3 (Mar 2021) - FHIR public preview with workspace with consumption model.

Since we want to introduce the consumption model first, we will prioritize that over dedicated model.

DICOM private preview MVP:

- Deploy 1 SQL server and 1 Elastic Pool per region. We will have limit of up to 500 database and up to 4TB shared data size.
- Provide monitoring and alerting for Elastic Pool utilization. 
- Provision database into the previous elastic pool with managed identity.

FHIR private preview MVP:

Even though we don't have official have private preview for FHIR, I would still like to introduce the it as checkpoint.

- Deploy 1 SQL server and 1 Elastic Pool per region.
- Provide monitoring and alerting for Elastic Pool utilization. 
- Provision database into the previous elastic pool with local user account.

FHIR public preview MVP:

- Support multiple Elastic Pools.
- Provide TSG for DRI with the ability to create new Elastic Pool and move database between pools, turn on/off provisioning to specific Elastic Pools.
- Provide automated load balancing of databases between Elastic Pools, auto scale of Elastic Pool.
- Support schema migration.

FHIR GA MVP:

- Billing
- Multiple SQL servers
- Provide access to SQL performance metrics.

# Scenarios

Since the work is quite large, we will light up features in multiple phases.

## Phase 1 - Q2

- Provisions SQL database for FHIR/DICOM service (in this spec).
- Uses managed identity to access the database (in this spec).
- Uses local SQL user to access the database (in this spec).

- Deploys Azure SQL Server and Elastic Pool in each region as part of the deployment.
- Provides monitoring and alerting for Elastic Pool utilization.

## Phase 2 - Q3

- Supports multiple Elastic Pools in the same resource group.
    - Supports adding new Elastic Pools.
    - Supports the ability to turn on/off provisioning to a specific Elastic Pool.
- Provides DRI the ability to move databases between Elastic Pools.
- Supports schema migration.
- Queries and stores performance metrics for all SQL server/database.

## Phase 3 - Q3

- Supports Auto-scaling of Elastic Pool
- Supports provisioning to Elastic Pool based on heuristics (usage, data size, etc)
- Supports multiple Azure SQL server in the same resource group.
- Billing

## Future

- Integration with Geneva Action for Elastic Pool management in case of emergency.
- BYOK
- Replication
- TBD

# Metrics

- Number of accounts provisioned with SQL database.
- Number of successes/failures provisioning a SQL database.
- Time it takes to provision a SQL database.

- Number of successes/failures credential rotations.
- Number of Unauthorized exception connecting to SQL database.

# Design

The actual infrastructure and deployment are still in flux/design phase so some of the design described below will be conceptual. Once the plans are more solidified, we can then convert these conceptual into more concrete work items.

## Azure SQL Server basics

### Azure SQL Server

Azure SQL Server is a logical server that can hold SQL Database or Elastic Pool. By itself, it does not cost anything since compute and storage are allocated at the SQL Database or Elastic Pool level.

There are various resource limit which will influence how we manage customer databases.

| Resource | Limit |
| :------- | :---- |
| Default number of servers per subscription in any region | 20 |
| Maximum number of servers per subscription in any region | 200 |
| Maximum vCore per server | 540 |
| Maximum number of databases per server | 5000 |
| Maximum number of Elastic Pool per server | Limited by number of vCores |

More detail can be found [here](https://docs.microsoft.com/en-us/azure/azure-sql/database/resource-limits-logical-server).

### Azure SQL Database

Azure SQL Database has dedicated compute and storage associated with it. It offers vCore and DTU model. vCore model should be preferred over the DTU model as it gives us more flexibility and transparency, and should allow us to manage and plan resource utilization better.

Resource limit using vCore model can be found [here](https://docs.microsoft.com/en-us/azure/azure-sql/database/resource-limits-vcore-single-databases).

The General Purpose Gen 5 can be configured from 2 vCore up to 80 vCore with associated data size from 512GB to 4TB.

Single database is ideal for dedicated model where we want to preserve the compute and be isolated from noisy neighbor problem.

Because individual Azure SQL Database is associated with an instance of Azure SQL Server, the database must reside in the same resource group as the server instance.

### Elastic Pool

Elastic Pool has compute and storage that can be shared by multiple databases.

Resource limit using vCore model can be found [here](https://docs.microsoft.com/en-us/azure/azure-sql/database/resource-limits-vcore-elastic-pools)

The General Purpose Gen 5 Elastic Pool can be configured from 2 vCore up to 80 vCore with associated data size from 512GB to 4TB.

Because individual Elastic Pool is associated with an instance of Azure SQL Server, the pool must also reside in the same resource group as the server instance.

### Moving database

There are times where we will need to move the databases around for load balancing, upgrading the account from lower tier to dedicated tier (by moving the database out of Elastic Pool to single-instance database), or for any other reasons.

#### Moving database into, out of, or between Elastic Pools within the same resource group

Moving the database into, out of, or between Elastic Pools within the same resource group is pretty easy. This can be done by setting the Elastic Pool resource id during create or update of an Azure SQL Database.

https://docs.microsoft.com/en-us/rest/api/sql/databases/createorupdate

Assuming we have a SQL server instance named `test-server` and Elastic Pools named `test-pool-1` and `test-pool-2`, the following example shows how to move the database around:

``` powershell
$resourceGroupName = "test-rg"
$serverName = "test-server"
$elasticPoolName1 = "test-pool-1"
$elasticPoolName2 = "test-pool-2"
$databaseName = "test-db"

# Create a new database without associating with any Elastic Pool
New-AzSqlDatabase -ResourceGroupName $resourceGroupName `
    -ServerName $serverName `
    -DatabaseName $databaseName `

# Move the database into Elastic Pool 1
Set-AzSqlDatabase -ResourceGroupName $resourceGroupName `
    -ServerName $serverName `
    -ElasticPoolName $elasticPoolName1 `
    -DatabaseName $databaseName

# Move the database into Elastic Pool 2
Set-AzSqlDatabase -ResourceGroupName $resourceGroupName `
    -ServerName $serverName `
    -ElasticPoolName $elasticPoolName2 `
    -DatabaseName $databaseName

# Move the database out of Elastic Pool
Set-AzSqlDatabase -ResourceGroupName $resourceGroupName `
    -ServerName $serverName `
    -DatabaseName $databaseName `
    -RequestedServiceObjectiveName "S0"
```

To see how long the move will take and what kind of interruption the database would experience, I did an experiment with moving a database of 50GB between two Elastic Pools. It took about 18 seconds to complete the move operation. A simple query was executed against the database in a loop, which averaged 70 ms. After move was initiated, one of the query took 2 seconds to complete but went down to its normal average immediately.

#### Moving database between Azure SQL servers

Moving a database to another SQL server involves a little bit more gymnastics but can be done in the following manner by using Geo-Replication.

Assuming we have a SQL server instance named `test-server-1` with Elastic Pool named `test-pool-1` in resource group named `test-rg-1` and another SQL server instance named `test-server-2` with Elastic Pool named `test-pool-2` in resource group named `test-rg-2`, the following example shows how to move the database from `test-rg-1` to `test-rg-2`:

``` powershell
$primaryResourceGroupName = "test-rg-1"
$primaryServerName = "test-server-1"
$primaryElasticPoolName = "test-pool-1"

$secondaryResourceGroupName = "test-rg-2"
$secondaryServerName = "test-server-2"
$secondaryElasticPoolName = "test-pool-2"

$databaseName = "test-db"

# Create the primary database
$database - New-AzSqlDatabase -ResourceGroupName $primaryResourceGroupName `
    -ServerName $primaryServerName `
    -ElasticPoolName $primaryElasticPoolName `
    -DatabaseName $databaseName

# Setup secondary database (the name of the secondary database will be the same as the primary)
$database | New-AzSqlDatabaseSecondary -PartnerResourceGroupName $secondaryResourceGroupName `
    -PartnerServerName $secondaryServerName `
    -SecondaryElasticPoolName $secondaryElasticPoolName

# Get the secondary database
$database2 = Get-AzSqlDatabase -ResourceGroupName $secondaryResourceGroupName `
    -ServerName $secondaryServerName `
    -DatabaseName $databaseName

# Initiate failover
$database2 | Set-AzSqlDatabaseSecondary -PartnerResourceGroupName $primaryResourceGroupName `
    -Failover

# Monitor failover
$link = $database2 | Get-AzSqlDatabaseReplicationLink -PartnerResourceGroupName $primaryResourceGroupName `
    -PartnerServerName $primaryServerName

# Wait until failover is complete then remove the old database
```
The step is the same for single database by just removing all of the Elastic Pool parameters from previous steps.

I did the same experiment by moving a database with 50GB between two SQL servers in South Central US. Setting up the secondary database took about 6.5 minutes and the failover took about 25 seconds. During the failover, around 10 seconds or so, the queries were failing.

Out of curiosity, I also did the same experiment but between a SQL servers in South Central US and Southeast Asia. Setting up the secondary database took about 21 minutes and failover took about 44 seconds. During the failover, there were no failure in queries, but one of the query took around 80 seconds.

#### Scaling the Elastic Pool

I did some quick testing to see how long it takes to scale Elastic Pool and whether it depends on the number of databases it has. 

__Scaling compute__

| Databases | Operation            | Time             |
| :-------- | :------------------- | :--------------- |
| 1  x 50GB | 4  vCore -> 20 vCore | 1 min 51 seconds |
| 1  x 50GB | 16 vCore -> 4  vCore | 1 min 41 seconds |
| 3  x 50GB | 2  vCore -> 8  vCore | 1 min 30 seconds |
| 3  x 50GB | 8  vCore -> 16 vCore | 1 min 40 seconds |
| 10 x 50GB | 8  vCore -> 16 vCore | 2 min 43 seconds |
| 10 x 50GB | 20 vCore -> 40 vCore | 2 min 46 seconds |
| 10 x 50GB | 40 vCore -> 8  vCore | 2 min 43 seconds |
| 20 x 50GB | 16 vCore -> 32 vCore | 3 min 58 seconds |
| 20 x 50GB | 32 vCore -> 80 vCore | 4 min 02 seconds |
| 20 x 50GB | 80 vCore -> 16 vCore | 3 min 49 seconds |

Based on the results, the time it takes to scale seems to depends on either the number of databases the Elastic Pool has or the amount of data it has. I haven't not done further investigation to see which it is.

__Scaling storage__

| Databases | Operation          | Time                  |
| :-------- | :----------------- | :-------------------- |
| 1  x 50GB | 50   GB -> 200  GB |            16 seconds |
| 3  x 50GB | 200  GB -> 500  GB |            18 seconds |
| 8  x 50GB | 500  GB -> 1000 GB |            52 seconds |
| 10 x 50GB | 1000 GB -> 2000 GB | 21 minutes 15 seconds |
| 10 x 50GB | 2000 GB -> 4000 GB |            17 seconds |
| 10 x 50GB | 4000 GB -> 2000 GB |            20 seconds |

Scaling up the storage seems to be relatively cheap. Scaling from 1TB to 2TB seems to be outlier but I think it might caused by the underlying infrastructure needing to reserve new resources. However, this tells us that even though these operations are relatively constant, we have to take into account the fact that some operations might occasionally takes long time to complete.

### Hyperscale

The maximum data size for both Azure SQL Database and Elastic Pool are 4TB. To store more than 4TB, we will need to use Hyperscale, which supports up to 100TB. Currently, Hyperscale is only supported for single instance database but support for Elastic Pool is coming.

## Deploying Azure SQL Server and Elastic Pool

For Phase 1, we will deploy Azure SQL Server and Elastic Pool as part of the deployment for each region. The SQL server will be associated with a region rather than cluster because the compute for a given customer might be load balanced between clusters within a region but the data should not need to move.

Eventually, the Elastic Pool should be created and deleted dynamically based on the usage but we will do that in the future.

### Provisioning database in an Elastic Pool

When a new SQL Database is provisioned, we need to determine what Elastic Pool it will go into. Initially, everything could go into the same Elastic Pool that was provisioned as part of deployment, but eventually, we will need to be able to dynamically create new Elastic Pools on demand.

TBD - Deepak

### Moving databases between Elastic Pools

TBD - Deepak

## Organizing resources in Azure

The resource group have a limit of 800 resources of the same resource type and since there is a maximum limit of 200 SQL servers per subscription quota, it will all fit within one resource group. Each SQL server can have up to 5000 databases but I have confirmed with the SQL team that that the databases do not count towards the limit within the resource group.

We can provision all of the SQL servers and its associated Elastic Pool and databases for a given region into one resource group or we can choose to have separate resource groups for each SQL servers. I am not sure if there is any significant performance difference in ARM calls.

## Manage access to database

We have a few options when it comes to how to manage access to individual databases. We can create local user login and user accounts but we will need to manage the password and rotate them periodically. SQL server supports managed identity, so we will explore that option as it simplifies portion of the workflows.

The recommendation is to use managed identity for AKS and local user login and accounts for SF.

To enable managed identity for SF application requires the application to be deployed through ARM; based on the few experiments we have done, it adds about 5 minutes to the provisioning time, which is not idea since it will take about 10 minutes to deploy the FHIR service.

### Creating a local administrator account

When a new SQL server instance is created, it must be created with a local administrator account. This requires both the username ans password to be supplied. 

We will use standard username such as `administrator` or `healthAdmin` (or could we generate a random username?) and generate the password on the fly during provisioning.

~~Both the username and password will be stored securely in a KeyVault. We can have one KeyVault instance per region to store all SQL server related secrets for that region.~~

Because we will add AAD administrator account to the SQL server, we technically don't need a local administrator account but SQL server requires it so we will generate random password and will not store the password. However, we will need to store the timestamp of when the password was last rotated somewhere. Perhaps we could store it as a tag on the SQL server itself so we don't need a separate storage for it. If we are storing metadata for load balancing anyways, we could also store this as metadata.

The access of KeyVault will be restricted to:

- Deployment agent (so that it can add new secret when a new SQL server is created initially)
- User RP (so that eventually the load balance service can auto-provision SQL server if needed)
- JIT contributor group (so that DRI can access the server if needed)

We should setup monitoring such that it creates IcM alerts whenever the secret is read by a user.

_Administrator account automatically have access to all of the databases including read/write permissions. We should see if it's possible modify the permission to only allow database operations._

Alternatively, since we will have AAD administrator account setup, we could just generate random password for the local administrator account and not store them at all. 

#### Rotating the credential

We will need rotate the local administrator password periodically. We will generate a random password, update it, and update the timestamp. Since we don't actually need this password anyway, if the process fails, we will just repeat the process again.

### Adding AAD administrator account

To manage database access (such as creating local login or grant managed identity access) from a service principal (such as User RP), we will need to set the AAD administrator account.

Because we can only have one AAD administrator account per SQL server, we will need to create an AAD group and assign that group as the administrator.

The AAD group will contain:

- User RP (so it can deploy databases into the server)
- Load balancing service (so it can move databases around)
- JIT group (so DRI can access the server if needed)

### Grant database access through managed identity

If we choose to create a managed identity for each FHIR/DICOM service instance, then we need to grant access of the account database to the appropriate identity.

https://docs.microsoft.com/en-us/azure/app-service/app-service-web-tutorial-connect-msi

[![](https://mermaid.ink/img/eyJjb2RlIjoic2VxdWVuY2VEaWFncmFtXG4gIFJQIFdvcmtlci0-PitEYXRhYmFzZSBQcm92aXNpb25pbmcgU2VydmljZTogQ3JlYXRlIGEgbmV3IGRhdGFiYXNlXG4gIERhdGFiYXNlIFByb3Zpc2lvbmluZyBTZXJ2aWNlLS0-Pi1SUCBXb3JrZXI6IERhdGFiYXNlIGNyZWF0ZWRcbiAgUlAgV29ya2VyLT4-K0RhdGFiYXNlOiBDcmVhdGUgYSBsb2NhbCB1c2VyXG4gIERhdGFiYXNlLS0-Pi1SUCBXb3JrZXI6IFVzZXIgY3JlYXRlZCIsIm1lcm1haWQiOnsidGhlbWUiOiJkZWZhdWx0Iiwic2VxdWVuY2UiOnsic2hvd1NlcXVlbmNlTnVtYmVycyI6dHJ1ZX0sInRoZW1lVmFyaWFibGVzIjp7ImJhY2tncm91bmQiOiJ3aGl0ZSIsInByaW1hcnlDb2xvciI6IiNFQ0VDRkYiLCJzZWNvbmRhcnlDb2xvciI6IiNmZmZmZGUiLCJ0ZXJ0aWFyeUNvbG9yIjoiaHNsKDgwLCAxMDAlLCA5Ni4yNzQ1MDk4MDM5JSkiLCJwcmltYXJ5Qm9yZGVyQ29sb3IiOiJoc2woMjQwLCA2MCUsIDg2LjI3NDUwOTgwMzklKSIsInNlY29uZGFyeUJvcmRlckNvbG9yIjoiaHNsKDYwLCA2MCUsIDgzLjUyOTQxMTc2NDclKSIsInRlcnRpYXJ5Qm9yZGVyQ29sb3IiOiJoc2woODAsIDYwJSwgODYuMjc0NTA5ODAzOSUpIiwicHJpbWFyeVRleHRDb2xvciI6IiMxMzEzMDAiLCJzZWNvbmRhcnlUZXh0Q29sb3IiOiIjMDAwMDIxIiwidGVydGlhcnlUZXh0Q29sb3IiOiJyZ2IoOS41MDAwMDAwMDAxLCA5LjUwMDAwMDAwMDEsIDkuNTAwMDAwMDAwMSkiLCJsaW5lQ29sb3IiOiIjMzMzMzMzIiwidGV4dENvbG9yIjoiIzMzMyIsIm1haW5Ca2ciOiIjRUNFQ0ZGIiwic2Vjb25kQmtnIjoiI2ZmZmZkZSIsImJvcmRlcjEiOiIjOTM3MERCIiwiYm9yZGVyMiI6IiNhYWFhMzMiLCJhcnJvd2hlYWRDb2xvciI6IiMzMzMzMzMiLCJmb250RmFtaWx5IjoiXCJ0cmVidWNoZXQgbXNcIiwgdmVyZGFuYSwgYXJpYWwiLCJmb250U2l6ZSI6IjE2cHgiLCJsYWJlbEJhY2tncm91bmQiOiIjZThlOGU4Iiwibm9kZUJrZyI6IiNFQ0VDRkYiLCJub2RlQm9yZGVyIjoiIzkzNzBEQiIsImNsdXN0ZXJCa2ciOiIjZmZmZmRlIiwiY2x1c3RlckJvcmRlciI6IiNhYWFhMzMiLCJkZWZhdWx0TGlua0NvbG9yIjoiIzMzMzMzMyIsInRpdGxlQ29sb3IiOiIjMzMzIiwiZWRnZUxhYmVsQmFja2dyb3VuZCI6IiNlOGU4ZTgiLCJhY3RvckJvcmRlciI6ImhzbCgyNTkuNjI2MTY4MjI0MywgNTkuNzc2NTM2MzEyOCUsIDg3LjkwMTk2MDc4NDMlKSIsImFjdG9yQmtnIjoiI0VDRUNGRiIsImFjdG9yVGV4dENvbG9yIjoiYmxhY2siLCJhY3RvckxpbmVDb2xvciI6ImdyZXkiLCJzaWduYWxDb2xvciI6IiMzMzMiLCJzaWduYWxUZXh0Q29sb3IiOiIjMzMzIiwibGFiZWxCb3hCa2dDb2xvciI6IiNFQ0VDRkYiLCJsYWJlbEJveEJvcmRlckNvbG9yIjoiaHNsKDI1OS42MjYxNjgyMjQzLCA1OS43NzY1MzYzMTI4JSwgODcuOTAxOTYwNzg0MyUpIiwibGFiZWxUZXh0Q29sb3IiOiJibGFjayIsImxvb3BUZXh0Q29sb3IiOiJibGFjayIsIm5vdGVCb3JkZXJDb2xvciI6IiNhYWFhMzMiLCJub3RlQmtnQ29sb3IiOiIjZmZmNWFkIiwibm90ZVRleHRDb2xvciI6ImJsYWNrIiwiYWN0aXZhdGlvbkJvcmRlckNvbG9yIjoiIzY2NiIsImFjdGl2YXRpb25Ca2dDb2xvciI6IiNmNGY0ZjQiLCJzZXF1ZW5jZU51bWJlckNvbG9yIjoid2hpdGUiLCJzZWN0aW9uQmtnQ29sb3IiOiJyZ2JhKDEwMiwgMTAyLCAyNTUsIDAuNDkpIiwiYWx0U2VjdGlvbkJrZ0NvbG9yIjoid2hpdGUiLCJzZWN0aW9uQmtnQ29sb3IyIjoiI2ZmZjQwMCIsInRhc2tCb3JkZXJDb2xvciI6IiM1MzRmYmMiLCJ0YXNrQmtnQ29sb3IiOiIjOGE5MGRkIiwidGFza1RleHRMaWdodENvbG9yIjoid2hpdGUiLCJ0YXNrVGV4dENvbG9yIjoid2hpdGUiLCJ0YXNrVGV4dERhcmtDb2xvciI6ImJsYWNrIiwidGFza1RleHRPdXRzaWRlQ29sb3IiOiJibGFjayIsInRhc2tUZXh0Q2xpY2thYmxlQ29sb3IiOiIjMDAzMTYzIiwiYWN0aXZlVGFza0JvcmRlckNvbG9yIjoiIzUzNGZiYyIsImFjdGl2ZVRhc2tCa2dDb2xvciI6IiNiZmM3ZmYiLCJncmlkQ29sb3IiOiJsaWdodGdyZXkiLCJkb25lVGFza0JrZ0NvbG9yIjoibGlnaHRncmV5IiwiZG9uZVRhc2tCb3JkZXJDb2xvciI6ImdyZXkiLCJjcml0Qm9yZGVyQ29sb3IiOiIjZmY4ODg4IiwiY3JpdEJrZ0NvbG9yIjoicmVkIiwidG9kYXlMaW5lQ29sb3IiOiJyZWQiLCJsYWJlbENvbG9yIjoiYmxhY2siLCJlcnJvckJrZ0NvbG9yIjoiIzU1MjIyMiIsImVycm9yVGV4dENvbG9yIjoiIzU1MjIyMiIsImNsYXNzVGV4dCI6IiMxMzEzMDAiLCJmaWxsVHlwZTAiOiIjRUNFQ0ZGIiwiZmlsbFR5cGUxIjoiI2ZmZmZkZSIsImZpbGxUeXBlMiI6ImhzbCgzMDQsIDEwMCUsIDk2LjI3NDUwOTgwMzklKSIsImZpbGxUeXBlMyI6ImhzbCgxMjQsIDEwMCUsIDkzLjUyOTQxMTc2NDclKSIsImZpbGxUeXBlNCI6ImhzbCgxNzYsIDEwMCUsIDk2LjI3NDUwOTgwMzklKSIsImZpbGxUeXBlNSI6ImhzbCgtNCwgMTAwJSwgOTMuNTI5NDExNzY0NyUpIiwiZmlsbFR5cGU2IjoiaHNsKDgsIDEwMCUsIDk2LjI3NDUwOTgwMzklKSIsImZpbGxUeXBlNyI6ImhzbCgxODgsIDEwMCUsIDkzLjUyOTQxMTc2NDclKSJ9fSwidXBkYXRlRWRpdG9yIjpmYWxzZX0)](https://mermaid-js.github.io/mermaid-live-editor/#/edit/eyJjb2RlIjoic2VxdWVuY2VEaWFncmFtXG4gIFJQIFdvcmtlci0-PitEYXRhYmFzZSBQcm92aXNpb25pbmcgU2VydmljZTogQ3JlYXRlIGEgbmV3IGRhdGFiYXNlXG4gIERhdGFiYXNlIFByb3Zpc2lvbmluZyBTZXJ2aWNlLS0-Pi1SUCBXb3JrZXI6IERhdGFiYXNlIGNyZWF0ZWRcbiAgUlAgV29ya2VyLT4-K0RhdGFiYXNlOiBDcmVhdGUgYSBsb2NhbCB1c2VyXG4gIERhdGFiYXNlLS0-Pi1SUCBXb3JrZXI6IFVzZXIgY3JlYXRlZCIsIm1lcm1haWQiOnsidGhlbWUiOiJkZWZhdWx0Iiwic2VxdWVuY2UiOnsic2hvd1NlcXVlbmNlTnVtYmVycyI6dHJ1ZX0sInRoZW1lVmFyaWFibGVzIjp7ImJhY2tncm91bmQiOiJ3aGl0ZSIsInByaW1hcnlDb2xvciI6IiNFQ0VDRkYiLCJzZWNvbmRhcnlDb2xvciI6IiNmZmZmZGUiLCJ0ZXJ0aWFyeUNvbG9yIjoiaHNsKDgwLCAxMDAlLCA5Ni4yNzQ1MDk4MDM5JSkiLCJwcmltYXJ5Qm9yZGVyQ29sb3IiOiJoc2woMjQwLCA2MCUsIDg2LjI3NDUwOTgwMzklKSIsInNlY29uZGFyeUJvcmRlckNvbG9yIjoiaHNsKDYwLCA2MCUsIDgzLjUyOTQxMTc2NDclKSIsInRlcnRpYXJ5Qm9yZGVyQ29sb3IiOiJoc2woODAsIDYwJSwgODYuMjc0NTA5ODAzOSUpIiwicHJpbWFyeVRleHRDb2xvciI6IiMxMzEzMDAiLCJzZWNvbmRhcnlUZXh0Q29sb3IiOiIjMDAwMDIxIiwidGVydGlhcnlUZXh0Q29sb3IiOiJyZ2IoOS41MDAwMDAwMDAxLCA5LjUwMDAwMDAwMDEsIDkuNTAwMDAwMDAwMSkiLCJsaW5lQ29sb3IiOiIjMzMzMzMzIiwidGV4dENvbG9yIjoiIzMzMyIsIm1haW5Ca2ciOiIjRUNFQ0ZGIiwic2Vjb25kQmtnIjoiI2ZmZmZkZSIsImJvcmRlcjEiOiIjOTM3MERCIiwiYm9yZGVyMiI6IiNhYWFhMzMiLCJhcnJvd2hlYWRDb2xvciI6IiMzMzMzMzMiLCJmb250RmFtaWx5IjoiXCJ0cmVidWNoZXQgbXNcIiwgdmVyZGFuYSwgYXJpYWwiLCJmb250U2l6ZSI6IjE2cHgiLCJsYWJlbEJhY2tncm91bmQiOiIjZThlOGU4Iiwibm9kZUJrZyI6IiNFQ0VDRkYiLCJub2RlQm9yZGVyIjoiIzkzNzBEQiIsImNsdXN0ZXJCa2ciOiIjZmZmZmRlIiwiY2x1c3RlckJvcmRlciI6IiNhYWFhMzMiLCJkZWZhdWx0TGlua0NvbG9yIjoiIzMzMzMzMyIsInRpdGxlQ29sb3IiOiIjMzMzIiwiZWRnZUxhYmVsQmFja2dyb3VuZCI6IiNlOGU4ZTgiLCJhY3RvckJvcmRlciI6ImhzbCgyNTkuNjI2MTY4MjI0MywgNTkuNzc2NTM2MzEyOCUsIDg3LjkwMTk2MDc4NDMlKSIsImFjdG9yQmtnIjoiI0VDRUNGRiIsImFjdG9yVGV4dENvbG9yIjoiYmxhY2siLCJhY3RvckxpbmVDb2xvciI6ImdyZXkiLCJzaWduYWxDb2xvciI6IiMzMzMiLCJzaWduYWxUZXh0Q29sb3IiOiIjMzMzIiwibGFiZWxCb3hCa2dDb2xvciI6IiNFQ0VDRkYiLCJsYWJlbEJveEJvcmRlckNvbG9yIjoiaHNsKDI1OS42MjYxNjgyMjQzLCA1OS43NzY1MzYzMTI4JSwgODcuOTAxOTYwNzg0MyUpIiwibGFiZWxUZXh0Q29sb3IiOiJibGFjayIsImxvb3BUZXh0Q29sb3IiOiJibGFjayIsIm5vdGVCb3JkZXJDb2xvciI6IiNhYWFhMzMiLCJub3RlQmtnQ29sb3IiOiIjZmZmNWFkIiwibm90ZVRleHRDb2xvciI6ImJsYWNrIiwiYWN0aXZhdGlvbkJvcmRlckNvbG9yIjoiIzY2NiIsImFjdGl2YXRpb25Ca2dDb2xvciI6IiNmNGY0ZjQiLCJzZXF1ZW5jZU51bWJlckNvbG9yIjoid2hpdGUiLCJzZWN0aW9uQmtnQ29sb3IiOiJyZ2JhKDEwMiwgMTAyLCAyNTUsIDAuNDkpIiwiYWx0U2VjdGlvbkJrZ0NvbG9yIjoid2hpdGUiLCJzZWN0aW9uQmtnQ29sb3IyIjoiI2ZmZjQwMCIsInRhc2tCb3JkZXJDb2xvciI6IiM1MzRmYmMiLCJ0YXNrQmtnQ29sb3IiOiIjOGE5MGRkIiwidGFza1RleHRMaWdodENvbG9yIjoid2hpdGUiLCJ0YXNrVGV4dENvbG9yIjoid2hpdGUiLCJ0YXNrVGV4dERhcmtDb2xvciI6ImJsYWNrIiwidGFza1RleHRPdXRzaWRlQ29sb3IiOiJibGFjayIsInRhc2tUZXh0Q2xpY2thYmxlQ29sb3IiOiIjMDAzMTYzIiwiYWN0aXZlVGFza0JvcmRlckNvbG9yIjoiIzUzNGZiYyIsImFjdGl2ZVRhc2tCa2dDb2xvciI6IiNiZmM3ZmYiLCJncmlkQ29sb3IiOiJsaWdodGdyZXkiLCJkb25lVGFza0JrZ0NvbG9yIjoibGlnaHRncmV5IiwiZG9uZVRhc2tCb3JkZXJDb2xvciI6ImdyZXkiLCJjcml0Qm9yZGVyQ29sb3IiOiIjZmY4ODg4IiwiY3JpdEJrZ0NvbG9yIjoicmVkIiwidG9kYXlMaW5lQ29sb3IiOiJyZWQiLCJsYWJlbENvbG9yIjoiYmxhY2siLCJlcnJvckJrZ0NvbG9yIjoiIzU1MjIyMiIsImVycm9yVGV4dENvbG9yIjoiIzU1MjIyMiIsImNsYXNzVGV4dCI6IiMxMzEzMDAiLCJmaWxsVHlwZTAiOiIjRUNFQ0ZGIiwiZmlsbFR5cGUxIjoiI2ZmZmZkZSIsImZpbGxUeXBlMiI6ImhzbCgzMDQsIDEwMCUsIDk2LjI3NDUwOTgwMzklKSIsImZpbGxUeXBlMyI6ImhzbCgxMjQsIDEwMCUsIDkzLjUyOTQxMTc2NDclKSIsImZpbGxUeXBlNCI6ImhzbCgxNzYsIDEwMCUsIDk2LjI3NDUwOTgwMzklKSIsImZpbGxUeXBlNSI6ImhzbCgtNCwgMTAwJSwgOTMuNTI5NDExNzY0NyUpIiwiZmlsbFR5cGU2IjoiaHNsKDgsIDEwMCUsIDk2LjI3NDUwOTgwMzklKSIsImZpbGxUeXBlNyI6ImhzbCgxODgsIDEwMCUsIDkzLjUyOTQxMTc2NDclKSJ9fSwidXBkYXRlRWRpdG9yIjpmYWxzZX0)

&nbsp;&nbsp;&nbsp;&nbsp;1\. The Database Provisioning Service abstracts out the logic for determining where the database is provisioned. Because the database name only needs to be unique within an instance of SQL server, this step needs to be deterministic even if we have multiple SQL servers.

&nbsp;&nbsp;&nbsp;&nbsp;3\. Open a SQL connection to the account specific database; Run the following T-SQL to create the user account using the managed identity and grant access to the database.
    
``` sql
CREATE USER [<identity-name>] FROM EXTERNAL PROVIDER;

ALTER ROLE db_datareader ADD MEMBER [<identity-name>];
ALTER ROLE db_datawriter ADD MEMBER [<identity-name>];
```

The `<identity-name>` is the name of the managed identity in Azure AD.

To connect to the SQL database using the managed identity, first we need to install the `Microsoft.Azure.Services.AppAuthentication` NuGet package. The authentication information can be excluded from the connection string.

``` csharp
string token = await new AzureServiceTokenProvider().GetAccessTokenAsync("https://database.windows.net/");

SqlConnection sqlConnection = new SqlConnection("Server=tcp:<server-name>.database.windows.net,1433;Database=<database-name>")
{
    AccessToken = token,
};

using (sqlConnection)
{
    await sqlConnection.OpenAsync();
}
```

The step will be slightly different depending on the hosting environment. For SF, we will use [Managed Identity Token Service](https://docs.microsoft.com/en-us/azure/service-fabric/how-to-managed-identity-service-fabric-app-code). For AKS, we will use [AAD Pod Identity](https://github.com/Azure/aad-pod-identity).

#### Caching the token

By default, the access token is valid for 1 hour. The `Microsoft.Azure.Services.AppAuthentication` library caches the token internally and automatically triggers refresh in the background when less than 5 minutes remaining until expiration.

Since we will need to call to one of the internal services to acquire access token ourselves, we will need to implement similar caching mechanism for optimization. 

#### Handling Unauthorized error

When the SQL connection or command is rejected with Unauthorized, we will try to refresh the token on-demand and retry the connection because it is possible that the certificate was revoke due to leak. If it still fails, then something has gone wrong and we should generate an alert and fail the request.

The code should be reusable for any services which wishes to acquire managed identity access tokens.

Because we must support handling on-demand refresh of the credential anyways, we could start with this and add support for background poll later. The downside of only using on-demand refresh is that the request latency will be longer but because credential rotation does not happen that frequently, it should have minimum impact.

### Grant database access through local user account

If we cannot use managed identity (as this might be the case for SF since deploying SF application with MI enabled adds at least 5 minutes to deployment time), then we will need to create local user account and grant database access through that.

The following sequence diagram shows the happy path of this flow (walk-through of the failure scenarios will be described separately).

[![](https://mermaid.ink/img/eyJjb2RlIjoic2VxdWVuY2VEaWFncmFtXG4gIFJQIFdvcmtlci0-PitLZXlWYXVsdDogR2V0IHNlY3JldCBidW5kbGVcbiAgS2V5VmF1bHQtLT4-LVJQIFdvcmtlcjogU2VjcmV0IGJ1bmRsZSBkb2VzIG5vdCBleGlzdFxuICBSUCBXb3JrZXItPj4rUlAgV29ya2VyOiBHZW5lcmF0ZSB1c2VybmFtZSBhbmQgYSByYW5kb20gcGFzc3dvcmRcbiAgUlAgV29ya2VyLT4-K0tleVZhdWx0OiBDcmVhdGUgc2VjcmV0IGJ1bmRsZVxuICBLZXlWYXVsdC0tPj4tUlAgV29ya2VyOiBTZWNyZXQgYnVuZGxlIGNyZWF0ZWRcbiAgUlAgV29ya2VyLT4-K0RhdGFiYXNlOiBDcmVhdGUgYSBsb2NhbCB1c2VyXG4gIERhdGFiYXNlLS0-Pi1SUCBXb3JrZXI6IFVzZXIgY3JlYXRlZFxuICBSUCBXb3JrZXItPj4rR2xvYmFsIERCOiBDcmVhdGUgYnVuZGxlIG1ldGFkYXRhIGluZm9ybWF0aW9uXG4gIEdsb2JhbCBEQi0tPj4tUlAgV29ya2VyOiBCdW5kbGUgbWV0YWRhdGEgaW5mb3JtYXRpb24gY3JlYXRlZCIsIm1lcm1haWQiOnsidGhlbWUiOiJkZWZhdWx0Iiwic2VxdWVuY2UiOnsic2hvd1NlcXVlbmNlTnVtYmVycyI6dHJ1ZX0sInRoZW1lVmFyaWFibGVzIjp7ImJhY2tncm91bmQiOiJ3aGl0ZSIsInByaW1hcnlDb2xvciI6IiNFQ0VDRkYiLCJzZWNvbmRhcnlDb2xvciI6IiNmZmZmZGUiLCJ0ZXJ0aWFyeUNvbG9yIjoiaHNsKDgwLCAxMDAlLCA5Ni4yNzQ1MDk4MDM5JSkiLCJwcmltYXJ5Qm9yZGVyQ29sb3IiOiJoc2woMjQwLCA2MCUsIDg2LjI3NDUwOTgwMzklKSIsInNlY29uZGFyeUJvcmRlckNvbG9yIjoiaHNsKDYwLCA2MCUsIDgzLjUyOTQxMTc2NDclKSIsInRlcnRpYXJ5Qm9yZGVyQ29sb3IiOiJoc2woODAsIDYwJSwgODYuMjc0NTA5ODAzOSUpIiwicHJpbWFyeVRleHRDb2xvciI6IiMxMzEzMDAiLCJzZWNvbmRhcnlUZXh0Q29sb3IiOiIjMDAwMDIxIiwidGVydGlhcnlUZXh0Q29sb3IiOiJyZ2IoOS41MDAwMDAwMDAxLCA5LjUwMDAwMDAwMDEsIDkuNTAwMDAwMDAwMSkiLCJsaW5lQ29sb3IiOiIjMzMzMzMzIiwidGV4dENvbG9yIjoiIzMzMyIsIm1haW5Ca2ciOiIjRUNFQ0ZGIiwic2Vjb25kQmtnIjoiI2ZmZmZkZSIsImJvcmRlcjEiOiIjOTM3MERCIiwiYm9yZGVyMiI6IiNhYWFhMzMiLCJhcnJvd2hlYWRDb2xvciI6IiMzMzMzMzMiLCJmb250RmFtaWx5IjoiXCJ0cmVidWNoZXQgbXNcIiwgdmVyZGFuYSwgYXJpYWwiLCJmb250U2l6ZSI6IjE2cHgiLCJsYWJlbEJhY2tncm91bmQiOiIjZThlOGU4Iiwibm9kZUJrZyI6IiNFQ0VDRkYiLCJub2RlQm9yZGVyIjoiIzkzNzBEQiIsImNsdXN0ZXJCa2ciOiIjZmZmZmRlIiwiY2x1c3RlckJvcmRlciI6IiNhYWFhMzMiLCJkZWZhdWx0TGlua0NvbG9yIjoiIzMzMzMzMyIsInRpdGxlQ29sb3IiOiIjMzMzIiwiZWRnZUxhYmVsQmFja2dyb3VuZCI6IiNlOGU4ZTgiLCJhY3RvckJvcmRlciI6ImhzbCgyNTkuNjI2MTY4MjI0MywgNTkuNzc2NTM2MzEyOCUsIDg3LjkwMTk2MDc4NDMlKSIsImFjdG9yQmtnIjoiI0VDRUNGRiIsImFjdG9yVGV4dENvbG9yIjoiYmxhY2siLCJhY3RvckxpbmVDb2xvciI6ImdyZXkiLCJzaWduYWxDb2xvciI6IiMzMzMiLCJzaWduYWxUZXh0Q29sb3IiOiIjMzMzIiwibGFiZWxCb3hCa2dDb2xvciI6IiNFQ0VDRkYiLCJsYWJlbEJveEJvcmRlckNvbG9yIjoiaHNsKDI1OS42MjYxNjgyMjQzLCA1OS43NzY1MzYzMTI4JSwgODcuOTAxOTYwNzg0MyUpIiwibGFiZWxUZXh0Q29sb3IiOiJibGFjayIsImxvb3BUZXh0Q29sb3IiOiJibGFjayIsIm5vdGVCb3JkZXJDb2xvciI6IiNhYWFhMzMiLCJub3RlQmtnQ29sb3IiOiIjZmZmNWFkIiwibm90ZVRleHRDb2xvciI6ImJsYWNrIiwiYWN0aXZhdGlvbkJvcmRlckNvbG9yIjoiIzY2NiIsImFjdGl2YXRpb25Ca2dDb2xvciI6IiNmNGY0ZjQiLCJzZXF1ZW5jZU51bWJlckNvbG9yIjoid2hpdGUiLCJzZWN0aW9uQmtnQ29sb3IiOiJyZ2JhKDEwMiwgMTAyLCAyNTUsIDAuNDkpIiwiYWx0U2VjdGlvbkJrZ0NvbG9yIjoid2hpdGUiLCJzZWN0aW9uQmtnQ29sb3IyIjoiI2ZmZjQwMCIsInRhc2tCb3JkZXJDb2xvciI6IiM1MzRmYmMiLCJ0YXNrQmtnQ29sb3IiOiIjOGE5MGRkIiwidGFza1RleHRMaWdodENvbG9yIjoid2hpdGUiLCJ0YXNrVGV4dENvbG9yIjoid2hpdGUiLCJ0YXNrVGV4dERhcmtDb2xvciI6ImJsYWNrIiwidGFza1RleHRPdXRzaWRlQ29sb3IiOiJibGFjayIsInRhc2tUZXh0Q2xpY2thYmxlQ29sb3IiOiIjMDAzMTYzIiwiYWN0aXZlVGFza0JvcmRlckNvbG9yIjoiIzUzNGZiYyIsImFjdGl2ZVRhc2tCa2dDb2xvciI6IiNiZmM3ZmYiLCJncmlkQ29sb3IiOiJsaWdodGdyZXkiLCJkb25lVGFza0JrZ0NvbG9yIjoibGlnaHRncmV5IiwiZG9uZVRhc2tCb3JkZXJDb2xvciI6ImdyZXkiLCJjcml0Qm9yZGVyQ29sb3IiOiIjZmY4ODg4IiwiY3JpdEJrZ0NvbG9yIjoicmVkIiwidG9kYXlMaW5lQ29sb3IiOiJyZWQiLCJsYWJlbENvbG9yIjoiYmxhY2siLCJlcnJvckJrZ0NvbG9yIjoiIzU1MjIyMiIsImVycm9yVGV4dENvbG9yIjoiIzU1MjIyMiIsImNsYXNzVGV4dCI6IiMxMzEzMDAiLCJmaWxsVHlwZTAiOiIjRUNFQ0ZGIiwiZmlsbFR5cGUxIjoiI2ZmZmZkZSIsImZpbGxUeXBlMiI6ImhzbCgzMDQsIDEwMCUsIDk2LjI3NDUwOTgwMzklKSIsImZpbGxUeXBlMyI6ImhzbCgxMjQsIDEwMCUsIDkzLjUyOTQxMTc2NDclKSIsImZpbGxUeXBlNCI6ImhzbCgxNzYsIDEwMCUsIDk2LjI3NDUwOTgwMzklKSIsImZpbGxUeXBlNSI6ImhzbCgtNCwgMTAwJSwgOTMuNTI5NDExNzY0NyUpIiwiZmlsbFR5cGU2IjoiaHNsKDgsIDEwMCUsIDk2LjI3NDUwOTgwMzklKSIsImZpbGxUeXBlNyI6ImhzbCgxODgsIDEwMCUsIDkzLjUyOTQxMTc2NDclKSJ9fSwidXBkYXRlRWRpdG9yIjpmYWxzZX0)](https://mermaid-js.github.io/mermaid-live-editor/#/edit/eyJjb2RlIjoic2VxdWVuY2VEaWFncmFtXG4gIFJQIFdvcmtlci0-PitLZXlWYXVsdDogR2V0IHNlY3JldCBidW5kbGVcbiAgS2V5VmF1bHQtLT4-LVJQIFdvcmtlcjogU2VjcmV0IGJ1bmRsZSBkb2VzIG5vdCBleGlzdFxuICBSUCBXb3JrZXItPj4rUlAgV29ya2VyOiBHZW5lcmF0ZSB1c2VybmFtZSBhbmQgYSByYW5kb20gcGFzc3dvcmRcbiAgUlAgV29ya2VyLT4-K0tleVZhdWx0OiBDcmVhdGUgc2VjcmV0IGJ1bmRsZVxuICBLZXlWYXVsdC0tPj4tUlAgV29ya2VyOiBTZWNyZXQgYnVuZGxlIGNyZWF0ZWRcbiAgUlAgV29ya2VyLT4-K0RhdGFiYXNlOiBDcmVhdGUgYSBsb2NhbCB1c2VyXG4gIERhdGFiYXNlLS0-Pi1SUCBXb3JrZXI6IFVzZXIgY3JlYXRlZFxuICBSUCBXb3JrZXItPj4rR2xvYmFsIERCOiBDcmVhdGUgYnVuZGxlIG1ldGFkYXRhIGluZm9ybWF0aW9uXG4gIEdsb2JhbCBEQi0tPj4tUlAgV29ya2VyOiBCdW5kbGUgbWV0YWRhdGEgaW5mb3JtYXRpb24gY3JlYXRlZCIsIm1lcm1haWQiOnsidGhlbWUiOiJkZWZhdWx0Iiwic2VxdWVuY2UiOnsic2hvd1NlcXVlbmNlTnVtYmVycyI6dHJ1ZX0sInRoZW1lVmFyaWFibGVzIjp7ImJhY2tncm91bmQiOiJ3aGl0ZSIsInByaW1hcnlDb2xvciI6IiNFQ0VDRkYiLCJzZWNvbmRhcnlDb2xvciI6IiNmZmZmZGUiLCJ0ZXJ0aWFyeUNvbG9yIjoiaHNsKDgwLCAxMDAlLCA5Ni4yNzQ1MDk4MDM5JSkiLCJwcmltYXJ5Qm9yZGVyQ29sb3IiOiJoc2woMjQwLCA2MCUsIDg2LjI3NDUwOTgwMzklKSIsInNlY29uZGFyeUJvcmRlckNvbG9yIjoiaHNsKDYwLCA2MCUsIDgzLjUyOTQxMTc2NDclKSIsInRlcnRpYXJ5Qm9yZGVyQ29sb3IiOiJoc2woODAsIDYwJSwgODYuMjc0NTA5ODAzOSUpIiwicHJpbWFyeVRleHRDb2xvciI6IiMxMzEzMDAiLCJzZWNvbmRhcnlUZXh0Q29sb3IiOiIjMDAwMDIxIiwidGVydGlhcnlUZXh0Q29sb3IiOiJyZ2IoOS41MDAwMDAwMDAxLCA5LjUwMDAwMDAwMDEsIDkuNTAwMDAwMDAwMSkiLCJsaW5lQ29sb3IiOiIjMzMzMzMzIiwidGV4dENvbG9yIjoiIzMzMyIsIm1haW5Ca2ciOiIjRUNFQ0ZGIiwic2Vjb25kQmtnIjoiI2ZmZmZkZSIsImJvcmRlcjEiOiIjOTM3MERCIiwiYm9yZGVyMiI6IiNhYWFhMzMiLCJhcnJvd2hlYWRDb2xvciI6IiMzMzMzMzMiLCJmb250RmFtaWx5IjoiXCJ0cmVidWNoZXQgbXNcIiwgdmVyZGFuYSwgYXJpYWwiLCJmb250U2l6ZSI6IjE2cHgiLCJsYWJlbEJhY2tncm91bmQiOiIjZThlOGU4Iiwibm9kZUJrZyI6IiNFQ0VDRkYiLCJub2RlQm9yZGVyIjoiIzkzNzBEQiIsImNsdXN0ZXJCa2ciOiIjZmZmZmRlIiwiY2x1c3RlckJvcmRlciI6IiNhYWFhMzMiLCJkZWZhdWx0TGlua0NvbG9yIjoiIzMzMzMzMyIsInRpdGxlQ29sb3IiOiIjMzMzIiwiZWRnZUxhYmVsQmFja2dyb3VuZCI6IiNlOGU4ZTgiLCJhY3RvckJvcmRlciI6ImhzbCgyNTkuNjI2MTY4MjI0MywgNTkuNzc2NTM2MzEyOCUsIDg3LjkwMTk2MDc4NDMlKSIsImFjdG9yQmtnIjoiI0VDRUNGRiIsImFjdG9yVGV4dENvbG9yIjoiYmxhY2siLCJhY3RvckxpbmVDb2xvciI6ImdyZXkiLCJzaWduYWxDb2xvciI6IiMzMzMiLCJzaWduYWxUZXh0Q29sb3IiOiIjMzMzIiwibGFiZWxCb3hCa2dDb2xvciI6IiNFQ0VDRkYiLCJsYWJlbEJveEJvcmRlckNvbG9yIjoiaHNsKDI1OS42MjYxNjgyMjQzLCA1OS43NzY1MzYzMTI4JSwgODcuOTAxOTYwNzg0MyUpIiwibGFiZWxUZXh0Q29sb3IiOiJibGFjayIsImxvb3BUZXh0Q29sb3IiOiJibGFjayIsIm5vdGVCb3JkZXJDb2xvciI6IiNhYWFhMzMiLCJub3RlQmtnQ29sb3IiOiIjZmZmNWFkIiwibm90ZVRleHRDb2xvciI6ImJsYWNrIiwiYWN0aXZhdGlvbkJvcmRlckNvbG9yIjoiIzY2NiIsImFjdGl2YXRpb25Ca2dDb2xvciI6IiNmNGY0ZjQiLCJzZXF1ZW5jZU51bWJlckNvbG9yIjoid2hpdGUiLCJzZWN0aW9uQmtnQ29sb3IiOiJyZ2JhKDEwMiwgMTAyLCAyNTUsIDAuNDkpIiwiYWx0U2VjdGlvbkJrZ0NvbG9yIjoid2hpdGUiLCJzZWN0aW9uQmtnQ29sb3IyIjoiI2ZmZjQwMCIsInRhc2tCb3JkZXJDb2xvciI6IiM1MzRmYmMiLCJ0YXNrQmtnQ29sb3IiOiIjOGE5MGRkIiwidGFza1RleHRMaWdodENvbG9yIjoid2hpdGUiLCJ0YXNrVGV4dENvbG9yIjoid2hpdGUiLCJ0YXNrVGV4dERhcmtDb2xvciI6ImJsYWNrIiwidGFza1RleHRPdXRzaWRlQ29sb3IiOiJibGFjayIsInRhc2tUZXh0Q2xpY2thYmxlQ29sb3IiOiIjMDAzMTYzIiwiYWN0aXZlVGFza0JvcmRlckNvbG9yIjoiIzUzNGZiYyIsImFjdGl2ZVRhc2tCa2dDb2xvciI6IiNiZmM3ZmYiLCJncmlkQ29sb3IiOiJsaWdodGdyZXkiLCJkb25lVGFza0JrZ0NvbG9yIjoibGlnaHRncmV5IiwiZG9uZVRhc2tCb3JkZXJDb2xvciI6ImdyZXkiLCJjcml0Qm9yZGVyQ29sb3IiOiIjZmY4ODg4IiwiY3JpdEJrZ0NvbG9yIjoicmVkIiwidG9kYXlMaW5lQ29sb3IiOiJyZWQiLCJsYWJlbENvbG9yIjoiYmxhY2siLCJlcnJvckJrZ0NvbG9yIjoiIzU1MjIyMiIsImVycm9yVGV4dENvbG9yIjoiIzU1MjIyMiIsImNsYXNzVGV4dCI6IiMxMzEzMDAiLCJmaWxsVHlwZTAiOiIjRUNFQ0ZGIiwiZmlsbFR5cGUxIjoiI2ZmZmZkZSIsImZpbGxUeXBlMiI6ImhzbCgzMDQsIDEwMCUsIDk2LjI3NDUwOTgwMzklKSIsImZpbGxUeXBlMyI6ImhzbCgxMjQsIDEwMCUsIDkzLjUyOTQxMTc2NDclKSIsImZpbGxUeXBlNCI6ImhzbCgxNzYsIDEwMCUsIDk2LjI3NDUwOTgwMzklKSIsImZpbGxUeXBlNSI6ImhzbCgtNCwgMTAwJSwgOTMuNTI5NDExNzY0NyUpIiwiZmlsbFR5cGU2IjoiaHNsKDgsIDEwMCUsIDk2LjI3NDUwOTgwMzklKSIsImZpbGxUeXBlNyI6ImhzbCgxODgsIDEwMCUsIDkzLjUyOTQxMTc2NDclKSJ9fSwidXBkYXRlRWRpdG9yIjpmYWxzZX0)

&nbsp;&nbsp;&nbsp;&nbsp;1\. Because KeyVault will simply create a new version of the secret, we will need to see if there was any previous attempt that failed middle way or not (if we don't want to create another copy of the secret). Since this section describes the happy path, we assume the secret does not exist.

&nbsp;&nbsp;&nbsp;&nbsp;3\. Generate the initial user name (e.g., `<account-id>-user-1`) and a random strong password (e.g., `<account-password-1>`).

&nbsp;&nbsp;&nbsp;&nbsp;4\. Store username and password as a JSON blob in KeyVault (e.g., secret name: `sql-<account-id>-connection-bundle`).

``` json
{
    "userName": "<account-id>-user-1",
    "password":"<account-password-1>",
    "lastUpdatedTimestamp":"<timestamp>"
}
```

&nbsp;&nbsp;&nbsp;&nbsp;6\. Open a SQL connection to the account specific database and run the following T-SQL to create the local user account with the information from previous step and grant access to the database.

``` sql
CREATE USER <account-id>-user-1
WITH PASSWORD = '<account-password-1>', DEFAULT_SCHEMA = dbo;

ALTER ROLE db_datareader ADD MEMBER <account-id>-user-1;
ALTER ROLE db_datawriter ADD MEMBER <account-id>-user-1;
```

&nbsp;&nbsp;&nbsp;&nbsp;8\. Write the SQL metadata information to the Global DB.
    
- We currently have document with type of `accountSpecificCosmosDbAccount` to store CosmosDB account metadata information so we will follow similar pattern and create `accountSpecificSqlDatabase` to store SQL metadata information.

``` json
{
    "type": "accountSpecificSqlDatabase",
    "secretBundleSecretName": "<account-id>-connectionInfo-bundle",
    "resourceGroup": "<account-id>-rg",
    "credentialLastRotated": "<timestamp>",
    "previousUsername": "<previous-user-name>", 
    "previousUserExpiresAt": "<timestamp>",
    "subscriptionId": "<subscription-id>",
    "partitionKey": "<subscription-id>",
    "name": "<account-id>",
    "id": "<account-id>",
}
```

- The `credentialLastRotated` property indicates when the credential was previous rotated.
- The `previousUsername` property is set to the previously username so the process can remove it after certain period of time. Once the previous account is successfully deleted, then the property will be set to null.
- The `previousUserExpiresAt` property indicates when the previous user account should be deleted.

For all of the steps above, we will retry for recoverable exceptions such as network error, but we also need to account for the fact that process might get terminated at any time so we need to have recovery plan for each step.

&nbsp;&nbsp;&nbsp;&nbsp;2\. If the secret bundle already exists, it means the previous attempt failed before step 8. Read the content of the JSON blob and reuse the username and password and skip to step 6.

&nbsp;&nbsp;&nbsp;&nbsp;5\. If creating the bundle fails with network error, then we need to retry from step 2 since the secret might have already been created.

&nbsp;&nbsp;&nbsp;&nbsp;7\. If creating a local user fails with user already exists, then the previous attempt failed on step 8. Continue to next step (or alternatively delete the user and recreate it).

&nbsp;&nbsp;&nbsp;&nbsp;9\. If this step does not complete, then provisioning operation will retry eventually.

#### Caching the connection string

Since the credential is not token based and therefore does not expire, we can cache it for the lifetime of the application. However, because the credential might be rotated in the background, we will need to poll for the update in the background and update the connection string if a new credential is available. This polling does not need to happen frequently so we can start with something like every hour.

When the background worker detects a new credential, it will update the configuration value. Any subsequent SQL connection will be created using the new credential.

#### Rotating the credential

To rotate the credential, we will generate a new user account and update the secret bundle. The old account will still be active for some period of time after the rotation as this will allow time for the service to poll the changes. After some threshold, the old account will be deleted from the database.

The following sequence diagram shows the happy path of this flow (walk-through of the failure scenarios will be described separately):

[![](https://mermaid.ink/img/eyJjb2RlIjoic2VxdWVuY2VEaWFncmFtXG4gIHJlY3QgcmdiYSgwLCAwLCAyNTUsIC4xKVxuICAgIHBhcnRpY2lwYW50IEtleVJvdGF0b3JcbiAgICBOb3RlIGxlZnQgb2YgS2V5Um90YXRvcjogUm90YXRlIGNyZWRlbnRpYWxcbiAgICBLZXlSb3RhdG9yLT4-K0FjY291bnRSZXBvc2l0b3J5OiBHZXQgbGlzdCBvZiBhY2NvdW50cyB3aGVyZSBjcmVkZW50aWFsIG5lZWRzIHRvIGJlIHJvdGF0ZWRcbiAgICBBY2NvdW50UmVwb3NpdG9yeS0tPj4tS2V5Um90YXRvcjogTGlzdCBvZiBhY2NvdW50c1xuICAgIGxvb3AgRm9yIGVhY2ggYWNjb3VudFxuICAgICAgS2V5Um90YXRvci0-PitLZXlWYXVsdDogR2V0IHRoZSBzZWNyZXQgYnVuZGxlIGZvciB0aGUgYWNjb3VudFxuICAgICAgS2V5VmF1bHQtLT4-LUtleVJvdGF0b3I6IFRoZSBzZWNyZXQgYnVuZGxlXG4gICAgICBhbHQgYGxhc3RVcGRhdGVkVGltZXN0YW1wYCA8PSBga2V5TGFzdFJvdGF0ZWRgXG4gICAgICAgIEtleVJvdGF0b3ItPj5LZXlSb3RhdG9yOiBHZW5lcmF0ZSBhbHRlcm5hdGUgdXNlcm5hbWUgYW5kIGEgcmFkb20gcGFzc3dvcmRcbiAgICAgICAgS2V5Um90YXRvci0-PitEYXRhYmFzZTogQ3JlYXRlIGEgbmV3IHVzZXIgd2l0aCBhIHJhbmRvbSBwYXNzd29yZFxuICAgICAgICBEYXRhYmFzZS0tPj4tS2V5Um90YXRvcjogVXNlciBjcmVhdGVkXG4gICAgICAgIEtleVJvdGF0b3ItPj4rS2V5VmF1bHQ6IFVwZGF0ZSB0aGUgc2VjcmV0IGJ1bmRsZSBmb3IgdGhlIGFjY291bnRcbiAgICAgICAgS2V5VmF1bHQtLT4-LUtleVJvdGF0b3I6IE5ldyBzZWNyZXQgdmVyc2lvblxuICAgICAgICBLZXlSb3RhdG9yLT4-K0FjY291bnRSZXBvc2l0b3J5OiBVcGRhdGUgdGhlIGFjY291bnRcbiAgICAgICAgQWNjb3VudFJlcG9zaXRvcnktLT4-LUtleVJvdGF0b3I6IEFjY291bnQgdXBkYXRlZFxuICAgICAgZW5kXG4gICAgZW5kXG4gIGVuZFxuICByZWN0IHJnYmEoMjU1LCAwLCAwLCAuMSlcbiAgICBOb3RlIGxlZnQgb2YgS2V5Um90YXRvcjogUmVtb3ZlIHByZXZpb3VzIGFjY291bnRcbiAgICBLZXlSb3RhdG9yLT4-K0FjY291bnRSZXBvc2l0b3J5OiBHZXQgbGlzdCBvZiBhY2NvdW50cyB3aGVyZSBwcmV2aW91cyBhY2NvdW50IG5lZWRzIHRvIGJlIHJlbW92ZWRcbiAgICBBY2NvdW50UmVwb3NpdG9yeS0tPj4tS2V5Um90YXRvcjogTGlzdCBvZiBhY2NvdW50c1xuICAgIGxvb3AgRm9yIGVhY2ggYWNjb3VudFxuICAgICAgS2V5Um90YXRvci0-PitEYXRhYmFzZTogUmVtb3ZlIHByZXZpb3VzIGFjY291bnRcbiAgICAgIERhdGFiYXNlLS0-Pi1LZXlSb3RhdG9yOiBVc2VyIHJlbW92ZWRcbiAgICAgIEtleVJvdGF0b3ItPj4rQWNjb3VudFJlcG9zaXRvcnk6IFVwZGF0ZSB0aGUgYWNjb3VudFxuICAgICAgQWNjb3VudFJlcG9zaXRvcnktLT4-LUtleVJvdGF0b3I6IEFjY291bnQgdXBkYXRlZFxuICAgIGVuZFxuICBlbmQiLCJtZXJtYWlkIjp7InRoZW1lIjoiZGVmYXVsdCIsInNlcXVlbmNlIjp7InNob3dTZXF1ZW5jZU51bWJlcnMiOnRydWV9LCJ0aGVtZVZhcmlhYmxlcyI6eyJiYWNrZ3JvdW5kIjoid2hpdGUiLCJwcmltYXJ5Q29sb3IiOiIjRUNFQ0ZGIiwic2Vjb25kYXJ5Q29sb3IiOiIjZmZmZmRlIiwidGVydGlhcnlDb2xvciI6ImhzbCg4MCwgMTAwJSwgOTYuMjc0NTA5ODAzOSUpIiwicHJpbWFyeUJvcmRlckNvbG9yIjoiaHNsKDI0MCwgNjAlLCA4Ni4yNzQ1MDk4MDM5JSkiLCJzZWNvbmRhcnlCb3JkZXJDb2xvciI6ImhzbCg2MCwgNjAlLCA4My41Mjk0MTE3NjQ3JSkiLCJ0ZXJ0aWFyeUJvcmRlckNvbG9yIjoiaHNsKDgwLCA2MCUsIDg2LjI3NDUwOTgwMzklKSIsInByaW1hcnlUZXh0Q29sb3IiOiIjMTMxMzAwIiwic2Vjb25kYXJ5VGV4dENvbG9yIjoiIzAwMDAyMSIsInRlcnRpYXJ5VGV4dENvbG9yIjoicmdiKDkuNTAwMDAwMDAwMSwgOS41MDAwMDAwMDAxLCA5LjUwMDAwMDAwMDEpIiwibGluZUNvbG9yIjoiIzMzMzMzMyIsInRleHRDb2xvciI6IiMzMzMiLCJtYWluQmtnIjoiI0VDRUNGRiIsInNlY29uZEJrZyI6IiNmZmZmZGUiLCJib3JkZXIxIjoiIzkzNzBEQiIsImJvcmRlcjIiOiIjYWFhYTMzIiwiYXJyb3doZWFkQ29sb3IiOiIjMzMzMzMzIiwiZm9udEZhbWlseSI6IlwidHJlYnVjaGV0IG1zXCIsIHZlcmRhbmEsIGFyaWFsIiwiZm9udFNpemUiOiIxNnB4IiwibGFiZWxCYWNrZ3JvdW5kIjoiI2U4ZThlOCIsIm5vZGVCa2ciOiIjRUNFQ0ZGIiwibm9kZUJvcmRlciI6IiM5MzcwREIiLCJjbHVzdGVyQmtnIjoiI2ZmZmZkZSIsImNsdXN0ZXJCb3JkZXIiOiIjYWFhYTMzIiwiZGVmYXVsdExpbmtDb2xvciI6IiMzMzMzMzMiLCJ0aXRsZUNvbG9yIjoiIzMzMyIsImVkZ2VMYWJlbEJhY2tncm91bmQiOiIjZThlOGU4IiwiYWN0b3JCb3JkZXIiOiJoc2woMjU5LjYyNjE2ODIyNDMsIDU5Ljc3NjUzNjMxMjglLCA4Ny45MDE5NjA3ODQzJSkiLCJhY3RvckJrZyI6IiNFQ0VDRkYiLCJhY3RvclRleHRDb2xvciI6ImJsYWNrIiwiYWN0b3JMaW5lQ29sb3IiOiJncmV5Iiwic2lnbmFsQ29sb3IiOiIjMzMzIiwic2lnbmFsVGV4dENvbG9yIjoiIzMzMyIsImxhYmVsQm94QmtnQ29sb3IiOiIjRUNFQ0ZGIiwibGFiZWxCb3hCb3JkZXJDb2xvciI6ImhzbCgyNTkuNjI2MTY4MjI0MywgNTkuNzc2NTM2MzEyOCUsIDg3LjkwMTk2MDc4NDMlKSIsImxhYmVsVGV4dENvbG9yIjoiYmxhY2siLCJsb29wVGV4dENvbG9yIjoiYmxhY2siLCJub3RlQm9yZGVyQ29sb3IiOiIjYWFhYTMzIiwibm90ZUJrZ0NvbG9yIjoiI2ZmZjVhZCIsIm5vdGVUZXh0Q29sb3IiOiJibGFjayIsImFjdGl2YXRpb25Cb3JkZXJDb2xvciI6IiM2NjYiLCJhY3RpdmF0aW9uQmtnQ29sb3IiOiIjZjRmNGY0Iiwic2VxdWVuY2VOdW1iZXJDb2xvciI6IndoaXRlIiwic2VjdGlvbkJrZ0NvbG9yIjoicmdiYSgxMDIsIDEwMiwgMjU1LCAwLjQ5KSIsImFsdFNlY3Rpb25Ca2dDb2xvciI6IndoaXRlIiwic2VjdGlvbkJrZ0NvbG9yMiI6IiNmZmY0MDAiLCJ0YXNrQm9yZGVyQ29sb3IiOiIjNTM0ZmJjIiwidGFza0JrZ0NvbG9yIjoiIzhhOTBkZCIsInRhc2tUZXh0TGlnaHRDb2xvciI6IndoaXRlIiwidGFza1RleHRDb2xvciI6IndoaXRlIiwidGFza1RleHREYXJrQ29sb3IiOiJibGFjayIsInRhc2tUZXh0T3V0c2lkZUNvbG9yIjoiYmxhY2siLCJ0YXNrVGV4dENsaWNrYWJsZUNvbG9yIjoiIzAwMzE2MyIsImFjdGl2ZVRhc2tCb3JkZXJDb2xvciI6IiM1MzRmYmMiLCJhY3RpdmVUYXNrQmtnQ29sb3IiOiIjYmZjN2ZmIiwiZ3JpZENvbG9yIjoibGlnaHRncmV5IiwiZG9uZVRhc2tCa2dDb2xvciI6ImxpZ2h0Z3JleSIsImRvbmVUYXNrQm9yZGVyQ29sb3IiOiJncmV5IiwiY3JpdEJvcmRlckNvbG9yIjoiI2ZmODg4OCIsImNyaXRCa2dDb2xvciI6InJlZCIsInRvZGF5TGluZUNvbG9yIjoicmVkIiwibGFiZWxDb2xvciI6ImJsYWNrIiwiZXJyb3JCa2dDb2xvciI6IiM1NTIyMjIiLCJlcnJvclRleHRDb2xvciI6IiM1NTIyMjIiLCJjbGFzc1RleHQiOiIjMTMxMzAwIiwiZmlsbFR5cGUwIjoiI0VDRUNGRiIsImZpbGxUeXBlMSI6IiNmZmZmZGUiLCJmaWxsVHlwZTIiOiJoc2woMzA0LCAxMDAlLCA5Ni4yNzQ1MDk4MDM5JSkiLCJmaWxsVHlwZTMiOiJoc2woMTI0LCAxMDAlLCA5My41Mjk0MTE3NjQ3JSkiLCJmaWxsVHlwZTQiOiJoc2woMTc2LCAxMDAlLCA5Ni4yNzQ1MDk4MDM5JSkiLCJmaWxsVHlwZTUiOiJoc2woLTQsIDEwMCUsIDkzLjUyOTQxMTc2NDclKSIsImZpbGxUeXBlNiI6ImhzbCg4LCAxMDAlLCA5Ni4yNzQ1MDk4MDM5JSkiLCJmaWxsVHlwZTciOiJoc2woMTg4LCAxMDAlLCA5My41Mjk0MTE3NjQ3JSkifX0sInVwZGF0ZUVkaXRvciI6ZmFsc2V9)](https://mermaid-js.github.io/mermaid-live-editor/#/edit/eyJjb2RlIjoic2VxdWVuY2VEaWFncmFtXG4gIHJlY3QgcmdiYSgwLCAwLCAyNTUsIC4xKVxuICAgIHBhcnRpY2lwYW50IEtleVJvdGF0b3JcbiAgICBOb3RlIGxlZnQgb2YgS2V5Um90YXRvcjogUm90YXRlIGNyZWRlbnRpYWxcbiAgICBLZXlSb3RhdG9yLT4-K0FjY291bnRSZXBvc2l0b3J5OiBHZXQgbGlzdCBvZiBhY2NvdW50cyB3aGVyZSBjcmVkZW50aWFsIG5lZWRzIHRvIGJlIHJvdGF0ZWRcbiAgICBBY2NvdW50UmVwb3NpdG9yeS0tPj4tS2V5Um90YXRvcjogTGlzdCBvZiBhY2NvdW50c1xuICAgIGxvb3AgRm9yIGVhY2ggYWNjb3VudFxuICAgICAgS2V5Um90YXRvci0-PitLZXlWYXVsdDogR2V0IHRoZSBzZWNyZXQgYnVuZGxlIGZvciB0aGUgYWNjb3VudFxuICAgICAgS2V5VmF1bHQtLT4-LUtleVJvdGF0b3I6IFRoZSBzZWNyZXQgYnVuZGxlXG4gICAgICBhbHQgYGxhc3RVcGRhdGVkVGltZXN0YW1wYCA8PSBga2V5TGFzdFJvdGF0ZWRgXG4gICAgICAgIEtleVJvdGF0b3ItPj5LZXlSb3RhdG9yOiBHZW5lcmF0ZSBhbHRlcm5hdGUgdXNlcm5hbWUgYW5kIGEgcmFkb20gcGFzc3dvcmRcbiAgICAgICAgS2V5Um90YXRvci0-PitEYXRhYmFzZTogQ3JlYXRlIGEgbmV3IHVzZXIgd2l0aCBhIHJhbmRvbSBwYXNzd29yZFxuICAgICAgICBEYXRhYmFzZS0tPj4tS2V5Um90YXRvcjogVXNlciBjcmVhdGVkXG4gICAgICAgIEtleVJvdGF0b3ItPj4rS2V5VmF1bHQ6IFVwZGF0ZSB0aGUgc2VjcmV0IGJ1bmRsZSBmb3IgdGhlIGFjY291bnRcbiAgICAgICAgS2V5VmF1bHQtLT4-LUtleVJvdGF0b3I6IE5ldyBzZWNyZXQgdmVyc2lvblxuICAgICAgICBLZXlSb3RhdG9yLT4-K0FjY291bnRSZXBvc2l0b3J5OiBVcGRhdGUgdGhlIGFjY291bnRcbiAgICAgICAgQWNjb3VudFJlcG9zaXRvcnktLT4-LUtleVJvdGF0b3I6IEFjY291bnQgdXBkYXRlZFxuICAgICAgZW5kXG4gICAgZW5kXG4gIGVuZFxuICByZWN0IHJnYmEoMjU1LCAwLCAwLCAuMSlcbiAgICBOb3RlIGxlZnQgb2YgS2V5Um90YXRvcjogUmVtb3ZlIHByZXZpb3VzIGFjY291bnRcbiAgICBLZXlSb3RhdG9yLT4-K0FjY291bnRSZXBvc2l0b3J5OiBHZXQgbGlzdCBvZiBhY2NvdW50cyB3aGVyZSBwcmV2aW91cyBhY2NvdW50IG5lZWRzIHRvIGJlIHJlbW92ZWRcbiAgICBBY2NvdW50UmVwb3NpdG9yeS0tPj4tS2V5Um90YXRvcjogTGlzdCBvZiBhY2NvdW50c1xuICAgIGxvb3AgRm9yIGVhY2ggYWNjb3VudFxuICAgICAgS2V5Um90YXRvci0-PitEYXRhYmFzZTogUmVtb3ZlIHByZXZpb3VzIGFjY291bnRcbiAgICAgIERhdGFiYXNlLS0-Pi1LZXlSb3RhdG9yOiBVc2VyIHJlbW92ZWRcbiAgICAgIEtleVJvdGF0b3ItPj4rQWNjb3VudFJlcG9zaXRvcnk6IFVwZGF0ZSB0aGUgYWNjb3VudFxuICAgICAgQWNjb3VudFJlcG9zaXRvcnktLT4-LUtleVJvdGF0b3I6IEFjY291bnQgdXBkYXRlZFxuICAgIGVuZFxuICBlbmQiLCJtZXJtYWlkIjp7InRoZW1lIjoiZGVmYXVsdCIsInNlcXVlbmNlIjp7InNob3dTZXF1ZW5jZU51bWJlcnMiOnRydWV9LCJ0aGVtZVZhcmlhYmxlcyI6eyJiYWNrZ3JvdW5kIjoid2hpdGUiLCJwcmltYXJ5Q29sb3IiOiIjRUNFQ0ZGIiwic2Vjb25kYXJ5Q29sb3IiOiIjZmZmZmRlIiwidGVydGlhcnlDb2xvciI6ImhzbCg4MCwgMTAwJSwgOTYuMjc0NTA5ODAzOSUpIiwicHJpbWFyeUJvcmRlckNvbG9yIjoiaHNsKDI0MCwgNjAlLCA4Ni4yNzQ1MDk4MDM5JSkiLCJzZWNvbmRhcnlCb3JkZXJDb2xvciI6ImhzbCg2MCwgNjAlLCA4My41Mjk0MTE3NjQ3JSkiLCJ0ZXJ0aWFyeUJvcmRlckNvbG9yIjoiaHNsKDgwLCA2MCUsIDg2LjI3NDUwOTgwMzklKSIsInByaW1hcnlUZXh0Q29sb3IiOiIjMTMxMzAwIiwic2Vjb25kYXJ5VGV4dENvbG9yIjoiIzAwMDAyMSIsInRlcnRpYXJ5VGV4dENvbG9yIjoicmdiKDkuNTAwMDAwMDAwMSwgOS41MDAwMDAwMDAxLCA5LjUwMDAwMDAwMDEpIiwibGluZUNvbG9yIjoiIzMzMzMzMyIsInRleHRDb2xvciI6IiMzMzMiLCJtYWluQmtnIjoiI0VDRUNGRiIsInNlY29uZEJrZyI6IiNmZmZmZGUiLCJib3JkZXIxIjoiIzkzNzBEQiIsImJvcmRlcjIiOiIjYWFhYTMzIiwiYXJyb3doZWFkQ29sb3IiOiIjMzMzMzMzIiwiZm9udEZhbWlseSI6IlwidHJlYnVjaGV0IG1zXCIsIHZlcmRhbmEsIGFyaWFsIiwiZm9udFNpemUiOiIxNnB4IiwibGFiZWxCYWNrZ3JvdW5kIjoiI2U4ZThlOCIsIm5vZGVCa2ciOiIjRUNFQ0ZGIiwibm9kZUJvcmRlciI6IiM5MzcwREIiLCJjbHVzdGVyQmtnIjoiI2ZmZmZkZSIsImNsdXN0ZXJCb3JkZXIiOiIjYWFhYTMzIiwiZGVmYXVsdExpbmtDb2xvciI6IiMzMzMzMzMiLCJ0aXRsZUNvbG9yIjoiIzMzMyIsImVkZ2VMYWJlbEJhY2tncm91bmQiOiIjZThlOGU4IiwiYWN0b3JCb3JkZXIiOiJoc2woMjU5LjYyNjE2ODIyNDMsIDU5Ljc3NjUzNjMxMjglLCA4Ny45MDE5NjA3ODQzJSkiLCJhY3RvckJrZyI6IiNFQ0VDRkYiLCJhY3RvclRleHRDb2xvciI6ImJsYWNrIiwiYWN0b3JMaW5lQ29sb3IiOiJncmV5Iiwic2lnbmFsQ29sb3IiOiIjMzMzIiwic2lnbmFsVGV4dENvbG9yIjoiIzMzMyIsImxhYmVsQm94QmtnQ29sb3IiOiIjRUNFQ0ZGIiwibGFiZWxCb3hCb3JkZXJDb2xvciI6ImhzbCgyNTkuNjI2MTY4MjI0MywgNTkuNzc2NTM2MzEyOCUsIDg3LjkwMTk2MDc4NDMlKSIsImxhYmVsVGV4dENvbG9yIjoiYmxhY2siLCJsb29wVGV4dENvbG9yIjoiYmxhY2siLCJub3RlQm9yZGVyQ29sb3IiOiIjYWFhYTMzIiwibm90ZUJrZ0NvbG9yIjoiI2ZmZjVhZCIsIm5vdGVUZXh0Q29sb3IiOiJibGFjayIsImFjdGl2YXRpb25Cb3JkZXJDb2xvciI6IiM2NjYiLCJhY3RpdmF0aW9uQmtnQ29sb3IiOiIjZjRmNGY0Iiwic2VxdWVuY2VOdW1iZXJDb2xvciI6IndoaXRlIiwic2VjdGlvbkJrZ0NvbG9yIjoicmdiYSgxMDIsIDEwMiwgMjU1LCAwLjQ5KSIsImFsdFNlY3Rpb25Ca2dDb2xvciI6IndoaXRlIiwic2VjdGlvbkJrZ0NvbG9yMiI6IiNmZmY0MDAiLCJ0YXNrQm9yZGVyQ29sb3IiOiIjNTM0ZmJjIiwidGFza0JrZ0NvbG9yIjoiIzhhOTBkZCIsInRhc2tUZXh0TGlnaHRDb2xvciI6IndoaXRlIiwidGFza1RleHRDb2xvciI6IndoaXRlIiwidGFza1RleHREYXJrQ29sb3IiOiJibGFjayIsInRhc2tUZXh0T3V0c2lkZUNvbG9yIjoiYmxhY2siLCJ0YXNrVGV4dENsaWNrYWJsZUNvbG9yIjoiIzAwMzE2MyIsImFjdGl2ZVRhc2tCb3JkZXJDb2xvciI6IiM1MzRmYmMiLCJhY3RpdmVUYXNrQmtnQ29sb3IiOiIjYmZjN2ZmIiwiZ3JpZENvbG9yIjoibGlnaHRncmV5IiwiZG9uZVRhc2tCa2dDb2xvciI6ImxpZ2h0Z3JleSIsImRvbmVUYXNrQm9yZGVyQ29sb3IiOiJncmV5IiwiY3JpdEJvcmRlckNvbG9yIjoiI2ZmODg4OCIsImNyaXRCa2dDb2xvciI6InJlZCIsInRvZGF5TGluZUNvbG9yIjoicmVkIiwibGFiZWxDb2xvciI6ImJsYWNrIiwiZXJyb3JCa2dDb2xvciI6IiM1NTIyMjIiLCJlcnJvclRleHRDb2xvciI6IiM1NTIyMjIiLCJjbGFzc1RleHQiOiIjMTMxMzAwIiwiZmlsbFR5cGUwIjoiI0VDRUNGRiIsImZpbGxUeXBlMSI6IiNmZmZmZGUiLCJmaWxsVHlwZTIiOiJoc2woMzA0LCAxMDAlLCA5Ni4yNzQ1MDk4MDM5JSkiLCJmaWxsVHlwZTMiOiJoc2woMTI0LCAxMDAlLCA5My41Mjk0MTE3NjQ3JSkiLCJmaWxsVHlwZTQiOiJoc2woMTc2LCAxMDAlLCA5Ni4yNzQ1MDk4MDM5JSkiLCJmaWxsVHlwZTUiOiJoc2woLTQsIDEwMCUsIDkzLjUyOTQxMTc2NDclKSIsImZpbGxUeXBlNiI6ImhzbCg4LCAxMDAlLCA5Ni4yNzQ1MDk4MDM5JSkiLCJmaWxsVHlwZTciOiJoc2woMTg4LCAxMDAlLCA5My41Mjk0MTE3NjQ3JSkifX0sInVwZGF0ZUVkaXRvciI6ZmFsc2V9)

__Rotate credential:__

&nbsp;&nbsp;&nbsp;&nbsp;1\. Retrieve list of `accountSpecificSqlDatabase` documents where the `credentialLastRotated` property is less than some threshold (e.g., today - 60 days).

&nbsp;&nbsp;&nbsp;&nbsp;4\. In the happy path, the `secret.lastUpdatedTimestamp` should be less than or equal to `account.credentialLastRotated`.

&nbsp;&nbsp;&nbsp;&nbsp;5\. Generate alternate username and new password.
    - Generate the alternate user name (e.g., `<account-id>-user-2` if the current user is `<account-id>-user-1` and `<account-id>-user-1` if the current user is `<account-id>-user-2`).
    - Generate new password `<account-password-2>`.

&nbsp;&nbsp;&nbsp;&nbsp;6\. Open a SQL connection to the account specific database and run the following T-SQL to create the alternative local user account with the username and password from previous step.
    
``` sql
CREATE USER <account-id>-user-2
WITH PASSWORD = '<account-password-2>', DEFAULT_SCHEMA = dbo;

ALTER ROLE db_datareader ADD MEMBER <account-id>-user-2;
ALTER ROLE db_datawriter ADD MEMBER <account-id>-user-2;
```

&nbsp;&nbsp;&nbsp;&nbsp;8\. Updates the secret bundle in KeyVault with `{"userName":"<account-id>-user-2", "password":"<account-password-2>", "lastUpdatedTimestamp":"<newTimestamp>"}`

&nbsp;&nbsp;&nbsp;&nbsp;10\. Update the `accountSpecificSqlDatabase` document and set `credentialLastRotated` = `<newTimestamp>`, `previousUserName` = `<account-id>-user-1`, `previousUserExpiresAt` = `<newTimestamp> + 2h`.
    - Since the service will be polling the credential update periodically, we want to allow the previous account to still be active for a small amount of time.

__Remove previous account:__

&nbsp;&nbsp;&nbsp;&nbsp;12\. Retrieve list of `accountSpecificSqlDatabase` documents where the `previousUserExpiresAt` property is less than or equal to now.

&nbsp;&nbsp;&nbsp;&nbsp;14\. Open a SQL connection to the account specific database and run the following T-SQL to delete the previous local user account.
    
``` sql
DROP USER <account-id>-user-1
```

&nbsp;&nbsp;&nbsp;&nbsp;16\. Update the `accountSpecificSqlDatabase` and set `previousUserName` = `null`, `previousUserExpiresAt` = `null`.

For all of the steps above, we will retry for recoverable exceptions such as network error, but we also need to account for the fact that process might get terminated at any time so we need to have recovery plan for each step.

&nbsp;&nbsp;&nbsp;&nbsp;4\. If the `secret.lastUpdatedTimestamp` is greater than `account.credentialLastRotated`, it means that previous attempt created a new account but failed on step 10. We will simply repeated step 10 with `credentialLastRotated` = `lastUpdatedTimestamp`, `previousUserName` = `<previousUserName>`, `previousUserExpiresAt` = `lastUpdatedTimestamp + 2h`.

&nbsp;&nbsp;&nbsp;&nbsp;7\. If creating the alternative local user account fails with account already exists, it means previous attempt created the account but failed on step 8. We will delete the account and recreate it with the new password.

&nbsp;&nbsp;&nbsp;&nbsp;9\. If creating the secret bundle fails due to network error, we need to execute from step 3 again because the bundle could have already been created.

&nbsp;&nbsp;&nbsp;&nbsp;11\. If updating the account fails due to network error, we can try again. If updating the account fails with precondition failed, it means something else has updated the account, we can reload the document and see if we can merge the change. If this step does not complete, the job will eventually try again since the `credentialLastRotated` will not be updated.

&nbsp;&nbsp;&nbsp;&nbsp;15\. If removing the previous account fails with user does not exist, then previous attempt failed at step 16. We can simply move onto next step.

&nbsp;&nbsp;&nbsp;&nbsp;17\. If updating the account fails due to network error, we can try again. If updating the account fails with precondition failed, it means something else has updated the account, we can reload the document and see if we can merge the change. If this step does not complete, the job will eventually try again since the `previousUserExpiresAt` will not be removed.

__Emergency rotation:__

We need to support emergency credential rotation in case of breach.

The step will be similar to previous except the existing account will be removed immediately. This will cause the subsequent connection and command to fail since the service might not have updated the credentials in the background but since we support on-demand credential refresh, the service will try to acquire the new credential within the same request.

We should consider expose this as an operation in ARM so that this can be triggered through Geneva Action eventually.

# Test Strategy

Unit tests will be written for all components.

To test the provisioning E2E scenarios, a new set of permutation will be added to the existing FHIR/DICOM E2E tests but with SQL as the persistent provider.

- Provision a new FHIR service and run E2E test against the service.
- Provision a new DICOM service using managed identity and run E2E tests against the service.

# Security

To test the local credential rotations, a new set of test cases will be added. (I just realized there is GlobalDB key rotation tests but no account specific key rotation tests?) We need a long-lived test account (which does not get de-provisioned after test is completed). 

- Verify that new version of the secret is added to the KeyVault and make sure.
- Verify the corresponding user in the latest version of the secret exists in database.
- Verify the previous user is active if `previousUserName` is not `null`.
- Verify the previous user is removed if `previousUserName` is `null`.
- Verify the emergency rotation endpoint immediate remove access from previous user and create new user. 

_To test the managed identity credential rotations, TBD (I need to do a bit more research to see how this can be done)._

# Other

Deployment script will be updated to include provisioning of a SQL server and Elastic Pool for each region (eventually this will be dynamic).

Provisioning script will be updated to include provisioning of FHIR service using CosmosDB and SQL database.
Testing script will be updated to include new E2E tests for SQL database.
