# SLA for Azure API for FHIR

**Azure API for FHIR** is a fully managed, standards-based, compliant API for clinical health data that enables you to create new systems of engagement for analytics, machine learning, and actionable intelligence with your health data. The service will be available 99.9% of time.

## Introduction

This Service Level Agreement for Microsoft Online Services (this “SLA”) is a part of your Microsoft volume licensing agreement (the “Agreement”). Capitalized terms used but not defined in this SLA will have the meaning assigned to them in the Agreement. This SLA applies to the Microsoft Online Services listed herein (a “Service” or the “Services”), but does not apply to separately branded services made available with or connected to the Services or to any on-premises software that is part of any Service.

If we do not achieve and maintain the Service Levels for each Service as described in this SLA, then you may be eligible for a credit towards a portion of your monthly service fees. We will not modify the terms of your SLA during the initial term of your subscription; however, if you renew your subscription, the version of this SLA that is current at the time of renewal will apply throughout your renewal term. We will provide at least 90 days’ notice for adverse material changes to this SLA.

## General Terms

### Definitions

**"Applicable Monthly Period"** means, for a calendar month in which a Service Credit is owed, the number of days that you are a subscriber for a Service.

**"Applicable Monthly Service Fees"** means the total fees actually paid by you for a Service that are applied to the month in which a Service Credit is owed.

**"Downtime"** is defined for each Service in the Services Specific Terms below.

**"Error Code"** means an indication that an operation has failed, such as an HTTP status code in the 5xx range.

**"External Connectivity"** is bi-directional network traffic over supported protocols such as HTTP and HTTPS that can be sent and received from a public IP address.

**"Incident"** means (i) any single event, or (ii) any set of events, that result in Downtime.

**"Management Portal"** means the web interface, provided by Microsoft, through which customers may manage the Service.

**"Service Credit"** is the percentage of the Applicable Monthly Service Fees credited to you following Microsoft’s claim approval.

**"Service Level"** means the performance metric(s) set forth in this SLA that Microsoft agrees to meet in the delivery of the Services.

**"Service Resource"** means an individual resource available for use within a Service.

**"Success Code"** means an indication that an operation has succeeded, such as an HTTP status code in the 2xx range.

**"Support Window"** refers to the period of time during which a Service feature or compatibility with a separate product or service is supported.

### Terms

**Claims**

In order for Microsoft to consider a claim, you must submit the claim to customer support at Microsoft Corporation including all information necessary for Microsoft to validate the claim, including but not limited to: (i) a detailed description of the Incident; (ii) information regarding the time and duration of the Downtime; (iii) the number and location(s) of affected users (if applicable); and (iv) descriptions of your attempts to resolve the Incident at the time of occurrence.

For a claim related to Microsoft Azure, we must receive the claim within two months of the end of the billing month in which the Incident that is the subject of the claim occurred. For claims related to all other Services, we must receive the claim by the end of the calendar month following the month in which the Incident occurred. For example, if the Incident occurred on February 15th, we must receive the claim and all required information by March 31st.

We will evaluate all information reasonably available to us and make a good faith determination of whether a Service Credit is owed. We will use commercially reasonable efforts to process claims during the subsequent month and within forty-five (45) days of receipt. You must be in compliance with the Agreement in order to be eligible for a Service Credit. If we determine that a Service Credit is owed to you, we will apply the Service Credit to your Applicable Monthly Service Fees.

If you purchased more than one Service (not as a suite), then you may submit claims pursuant to the process described above as if each Service were covered by an individual SLA. For example, if you purchased both Exchange Online and SharePoint Online (not as part of a suite), and during the term of the subscription an Incident caused Downtime for both Services, then you could be eligible for two separate Service Credits (one for each Service), by submitting two claims under this SLA. In the event that more than one Service Level for a particular Service is not met because of the same Incident, you must choose only one Service Level under which to make a claim based on the Incident. Unless as otherwise provided in a specific SLA, only one Service Credit is permitted per Service for an Applicable Monthly Period.

**Service Credits**

Service Credits are your sole and exclusive remedy for any performance or availability issues for any Service under the Agreement and this SLA. You may not unilaterally offset your Applicable Monthly Service Fees for any performance or availability issues.

Service Credits apply only to fees paid for the particular Service, Service Resource, or Service tier for which a Service Level has not been met. In cases where Service Levels apply to individual Service Resources or to separate Service tiers, Service Credits apply only to fees paid for the affected Service Resource or Service tier, as applicable. The Service Credits awarded in any billing month for a particular Service or Service Resource will not, under any circumstance, exceed your monthly service fees for that Service or Service Resource, as applicable, in the billing month.

If you purchased Services as part of a suite or other single offer, the Applicable Monthly Service Fees and Service Credit for each Service will be pro-rated.

If you purchased a Service from a reseller, you will receive a service credit directly from your reseller and the reseller will receive a Service Credit directly from us. The Service Credit will be based on the estimated retail price for the applicable Service, as determined by us in our reasonable discretion.

**Limitations**

This SLA and any applicable Service Levels do not apply to any performance or availability issues:

1. Due to factors outside our reasonable control (for example, natural disaster, war, acts of terrorism, riots, government action, or a network or device failure external to our data centers, including at your site or between your site and our data center);
2. That result from the use of services, hardware, or software not provided by us, including, but not limited to, issues resulting from inadequate bandwidth or related to third-party software or services;
3. Caused by your use of a Service after we advised you to modify your use of the Service, if you did not modify your use as advised;
4. During or with respect to preview, pre-release, beta or trial versions of a Service, feature or software (as determined by us) or to purchases made using Microsoft subscription credits;
5. That result from your unauthorized action or lack of action when required, or from your employees, agents, contractors, or vendors, or anyone gaining access to our network by means of your passwords or equipment, or otherwise resulting from your failure to follow appropriate security practices;
6. That result from your failure to adhere to any required configurations,use supported platforms, follow any policies for acceptable use, or your use of the Service in a manner inconsistent with the features and functionality of the Service (for example, attempts to perform operations that are not supported) or inconsistent with our published guidance
7. That result from faulty input, instructions, or arguments (for example,requests to access files that do not exist);
8. That result from your attempts to perform operations that exceed prescribed quotas or that resulted from our throttling of suspected abusive behavior;
9. Due to your use of Service features that are outside of associated Support Windows; or
10. For licenses reserved, but not paid for, at the time of the Incident.

Services purchased through Open, Open Value, and Open Value Subscription volume licensing agreements, and Services in an Office 365 Small Business Premium suite purchased in the form of a product key are not eligible for Service Credits based on service fees. For these Services, any Service Credit that you may be eligible for will be credited in the form of service time (i.e., days) as opposed to service fees, and any references to “Applicable Monthly Service Fees” is deleted and replaced by “Applicable Monthly Period.”

## SLA Detail

### Additional Definitions
"Total Transaction Attempts" is the total number of authenticated API requests made by Customer for a given Azure API for FHIR account during a billing month in a given Microsoft Azure subscription. Total Transaction Attempts do not include API requests that return an Error Code that are continuously repeated within a five-minute window after the first Error Code is received.

"Failed Transactions" is the set of all requests within Total Transaction Attempts that result in an Error Code or otherwise do not return a Success Code within 60 seconds after receipt by the Service.

### Monthly Uptime Calculation and Service Levels for Azure API for FHIR

"Monthly Uptime Percentage" for a given Azure API for FHIR account is calculated as Total Transaction Attempts less Failed Transactions divided by Total Transaction Attempts multiplied by 100. Monthly Uptime Percentage is represented by the following formula:

Monthly Uptime % = (Total Transaction Attempts - Failed Transactions) / Total Transaction Attempts X 100

The following Service Levels and Service Credits are applicable to Azure API for FHIR:

| Monthly Uptime Percentage | Service Credit |
|----|----|
| <99.9% | 10%|
|<99%|25%|

