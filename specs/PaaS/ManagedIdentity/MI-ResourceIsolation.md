Managed identity allows us to build more secure service and simplify credential management for our customers.

To understand more about managed identity, [What is managed identities for Azure resources?](https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview) is a great place to start. From the link:

> A common challenge when building cloud applications is how to manage the credentials in your code for authenticating to cloud services. Keeping the credentials secure is an important task. Ideally, the credentials never appear on developer workstations and aren't checked into source control. Azure Key Vault provides a way to securely store credentials, secrets, and other keys, but your code has to authenticate to Key Vault to retrieve them.
>
>The managed identities for Azure resources feature in Azure Active Directory (Azure AD) solves this problem. The feature provides Azure services with an automatically managed identity in Azure AD. You can use the identity to authenticate to any service that supports Azure AD authentication, including Key Vault, without any credentials in your code.

[[_TOC_]]

# Business Justification

*Explain WHY we are building this feature. For customer-facing features, which customers will use it?*

# Scenarios

As an engineer, I want to isolate each account such that access to resources is limited to individual account only.

# Metrics

*List the metrics/telemetry that should be collected. For example: number of accounts that use feature X, number of requests to Y API per hour/day/week. How do we measure the success of this feature?*

# Design

Since we deploy the FHIR server as a Service Fabric application, we would then require to be able to assign separate identity to each application. Unfortunately, Service Fabric today only support identity at VMSS level not at per application level.

I've talked to Service Fabric team and the support for assigning identity per application will be announced as preview feature at the end of June/early July.

# Test Strategy

*Describe the test strategy.*

# Security

*Describe any special security implications or security testing needed.*

# Other

*Describe any impact to privacy, localization, globalization, deployment, back-compat, SOPs, ISMS, etc.*
