We need to provide for additional SQL server creation in a region.

[[_TOC_]]

# Background

Currently there is just one SQL server provisioned per region. As its limits are being reached we need to add
more servers to ensure the continuing operation. This must be an automatic process excluding the need for any
manual intervention.

Once there is more than one server present an appropriate one will need to be selected for new account provisioning.
As well the load balancer will have to work with all available servers.

## Logical server limits

Per [the documentation][limits] the following are the logical SQL server limits:

Resource | Limit
--- | ---
Databases per logical server | 5000
Default number of logical servers per subscription in a region|20
Max number of logical servers per subscription in a region|250
DTU / eDTU quota per logical server|54,000
vCore quota per logical server|540
Max elastic pools per logical server|Limited by number of DTUs or vCores. For example, if each pool is 1000 DTUs, then a server can support 54 pools.

An important warning there states:

>As the number of databases approaches the limit per logical server, the following can occur:
>
>- Increasing latency in running queries against the master database. This includes views of resource utilization statistics such as sys.resource_stats.
>- Increasing latency in management operations and rendering portal viewpoints that involve enumerating databases in the server.

# Design

## Creation of additional SQL servers

### Triggering condition

Currently there is an [AzureResourceMonitor worker][azureresourcemonitor] that among other things is looking at
SQL servers and alerts on the following metrics from the [above table][section]:

- databases per logical server
- DTU, for which there appears to be a relationship to vCore and so both should be covered

**TODO:** the "logical servers per subscription in a region" is not currently being monitored and so is to be added
to the [AzureResourceMonitor][azureresourcemonitor].

**Note:** The procedure to request a quota increase is described [here][request-quota].

The goal of the new component is for the above monitor to never issue alerts. Hence the thresholds triggering
creation of a new SQL server should be lower.

### Actions of the new worker

- matches the documents of the type `sqlServer` in the global DB with the SQL servers present
  - documents have servers and vice versa
  - status of the servers is correct
  - take action if any of the above is wrong, e.g.:
    - if there is any discrepancy between what the document says and an actual state of the server
      then correct the document
    - if there is a missing document or a server then the system is "out of sync" in a sense and
      an alert is probably in order referencing a course of action to take (reprovisioning?)
- queries the same stats the [AzureResourceMonitor][azureresourcemonitor] does
- using lower thresholds creates a SQL server ensuring
  - SQL server is created using configuration values currently used by the deployment [script][deploy-sql-script]:
    - use the template-based approach
    - anything not covered by the template must not be missed, e.g.:
      - a new document of `sqlServer` type is created in the global DB where status might be dynamically set according
        to the status as the server creation progresses
      - any initial VA baselines

### Failures during attempts to create a server

We assume that attempts to create new servers will take place well before any threshold warranting an alert via
monitor arises. Given that it may not be a good idea to issue IcM during the first few failing attempts. However,
these first attempts may already come during some surge of activity and so the successful creation of the server
is crucial. Hence, the alerting action to take should be dependent on the "closeness" of the current state
that triggered the creation to the actual alert threshold of the monitor.

## Use of an optimal SQL server for new database provisioning

The provisioning code should use an optimal server to provision new databases in. The criteria to use:
- query all available servers for the stats similar to the ones being monitored and select the one with lower values.
  The assumption is that such a server will always be below thresholds. If any code checking the thresholds is close
  by (assuming it will be factored out and reused between the monitor and the worker) then an additional check here may
  not hurt.

## Load balancer awareness of all present SQL servers

The load balancer will be able to see all available SQL servers by querying `sqlServer` documents.
We need to ensure it either does it already or modify it accordingly.

# Test Strategy

The threshold relationship between the [AzureResourceMonitor][azureresourcemonitor] and the new component should
be ensured by either unit or integration tests, or both.

Full integration tests involving server creation may be taking long (up to 2 minutes for a server to be created
and appear in the portal) and so will have to be weighed against the definite increase in duration and possible
flakiness introduction.

During the development all the necessary actions of the component can be easily triggered and observed.

Examples:

- set artificially low thresholds to trigger server creation and happy path observation
- manipulate all of the below to observe recovering procedures
  - removing `sqlServer` document from the global
  - dropping SQL server referred to by the `sqlServer` document
  - mangling of the status in `sqlServer` document
  - any possible state of the SQL server itself not matching the status in the `sqlServer` document
- ensure load balancer picks up all servers
  - stress databases in different servers to see it in action

# Security

As the SQL server gets created in an existing environment the same security conditions should apply to it
automatically.

# Metrics

It is not immediately clear whether there can be any direct metric to measure effects. However, the following can be
considered an objectve way to appreciate the worker's value:
- a dashboard widget created listing the number of SQL servers per region with each server encapsulating current
  numbers that are monitored and acted upon by the new worker. Each server that has those numbers within configured
  values will signify success. The positive impact is assumed to be absence of negative effects
  [described in the documentation][limits].

# Other

No impact to localization, globalization, deployment, back-compat, SOPs, ISMS, is expected.

## Deployment of the initial SQL Server

The discussion showed the consensus is to proceed with the current procedure which creates the initial server 
via the deployment script.

In the future, the new server creating code can be promoted to also create an initial server - "on demand".

Various considerations in favor of one or the other approach can be examined by looking through previous versions of this
document in the pull request.

[limits]: https://docs.microsoft.com/en-us/azure/azure-sql/database/resource-limits-logical-server#logical-server-limits
[azureresourcemonitor]: https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=/src/ResourceProviderApplication/ResourceProviderWorker/AzureResourceMonitor.cs
[section]: #logical-server-limits
[deploy-sql-script]: https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=/deployment/scripts/Deploy-Sql.ps1
[aad-admin]: https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=/deployment/scripts/Deploy-Sql.ps1&version=GBmaster&line=50&lineEnd=50&lineStartColumn=14&lineEndColumn=51&lineStyle=plain&_a=contents
[request-quota]: https://docs.microsoft.com/en-us/azure/azure-sql/database/quota-increase-request