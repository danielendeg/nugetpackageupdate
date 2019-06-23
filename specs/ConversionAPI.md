Customers that wish to use the Azure API for FHIR or the Open Source FHIR Server for Azure will often need to convert legacy data formats (e.g., HL7 v2) to FHIR for ingestion into the FHIR service. It is difficult to create a one-size-fits-all data converter since implementation of standards such as HL7 v2 varies from organization to organization. We are aiming to build a templates based conversion engine that will allow customers to leverage a library of provided conversion templates and adjust them for their organizational needs.

The project will include a conversion API, a set of standard templates, and tooling for authoring templates (not covered in this document). This is an initial MVP implementation to enable testing with a few select customers. Based on the feedback we receive, a full production environment will be designed.

[[_TOC_]]

# Business Justification

The feedback received after the launch of the Azure API for FHIR has been consistent: customers need a way to convert legacy HL7 v2 data (and possibly other formats) to FHIR. They want a mapping tool that does not require deploying an integration engine (Mirth Connect, Iguana, Rhapsody, etc). There is an opportunity to provide such a conversion tool as an API service where we could charge based on number of API calls and offer a high availability service. The service would be consumed in the same manner as cognitive services (e.g., face api).

# Scenarios

* As a user, I want to convert incoming HL7 v2 message to bundles of FHIR resources and insert them into a FHIR server.
* As a user, I want to call a conversion API service from Logic Apps or Functions.
* As a user, I want to make custom changes to conversion templates and have the modified templates deployed and managed by the API service.

# Metrics

* Feedback from early customers.
* Number of conversion API calls.
* Conversion errors.
* Conversion time.
* CPU/Memory load.

# Design

The conversion API will be a multi-tenant (multi-account, see below) template based data conversion service. The service will:

1. Parse an incoming message to a structured format (json)
1. Use the parsed message as context for a [handlebars](http://handlebarsjs.com/) template conversion.
1. Return converted message.

Initially we will target HL7 v2 messages. The handlebars syntax allows for a template to be broken into [partials](http://handlebarsjs.com/partials.html) and the service would support that.

In addition to template conversion the service has to provide functionality for storing and modifying templates.

## Template format

Templates will use handlebars syntax. An example could look like:

```
{{#with (hl7v2GetSegments msg.v2 'PID' 'NK1')}}
{{#if PID}}
{
    "fullUrl": "urn:uuid:{{generateUUID (concat '/Patient?identifier=' PID.[0].[3].[0].[0])}}",
    "resource": {
        "resourceType": "Patient",
        "id": "{{generateUUID (concat '/Patient?identifier=' PID.[0].[3].[0].[0])}}",
        "identifier": [
            {{#if PID.[0].[3]}}
            {{>patientId-partial.hbs PID3=PID.[0].[3]}}
            {{/if}}
            {{#if PID.[0].[19]}},
            {
                "type": {
                    "coding": [
                        {
                        "system": "http://terminology.hl7.org/CodeSystem/v2-0203",
                        "code": "SS"
                        }
                    ],
                    "text": "SSN"
                },
                "value": "{{addHyphensSSN PID.[0].[19].[0].[0]}}",
                "system": "http://hl7.org/fhir/sid/us-ssn"
            }
            {{/if}}
            ],
        {{#with PID.[0].[7]}}
        "birthDate": "{{addHypensBirthDate @this}}",

        // More stuff for resource

    },
    "request": {
        "method": "POST",
        "url": "Patient",
        {{#if PID.[0].[3]}}
        "ifNoneExist": "identifier=http://terminology.hl7.org/CodeSystem/v2-0203|{{PID.[0].[3].[0].[0]}}"
        {{/if}}
    }
}
{{/if}}
{{/with}}
```

The format supports helper functions and partials. The service will provide a number of pre-registered helper functions that will assist with extracting information from the parsed messages.

## Accounts

The service will be able service multiple conversion accounts. An account is an entity for which a set of named conversion templates are maintained. Suppose account `123` has a template named `patient.hbs`, account `345` could also have a template named `patient.hbs`. Each account will be identified by a string (could be a guid). All operations on the API are done in the context of an account.

## URL Scheme

All calls to the API will contain the account identifier, specifically:

```
GET [base]/{account}
```

would be the way to prefix all calls for a specific conversion account, where `[base]` is for example `https://azure-conversion-api.com/` and `{account}` is the account identifier (e.g. `123` or a guid).

Specific operations and conversions would be available with something like:

```
GET [base]/{account}/templates
```

To get a list of templates or

```
GET [base]/{account}/templates/{template-name}
```

For a specific template (see below)

## Built-in templates

The service will come with a set of built-in templates. The names of built-in templates will be prefixed with `$`.

```
GET [base]/{account}/templates/$default
```

will reference a built-in template called `default`. Built-in templates cannot be modified and are the same for all accounts.

## Managing templates

### Upload (or update) a template

```
POST [base]/{account}/templates/{template-name}
```

The body will contain the template body. When a template is received the template engine will:

1. Replace references to partials with a reference to a partial associated with the current account. Specifically something like:
    ```
     {{>patientId-partial.hbs}}
    ```
    would be replaced with:
    ```
     {{>account-patientId-partial.hbs}}
    ```
    i.e., it will be prefixed with the account identifier.
1. Attempt to pre-compile (validate) the template.
1. Store the template in blob storage.

Possible responses are:
* `201`: Created, new template stored.
* `200`: OK, template updated.
* `400`: Bad request, with some message indicating why the template could not be compiled and/or stored.

### List templates

```
GET [base]/{account}/templates
```

Response:
* `200`: List of template names `[ '$default', 'custom1.hbs', 'custom2.hbs', ...]` including any default templates.

### Get template

```
GET [base]/{account}/templates/{template-name}
```

Response:
* `200`: OK, template returned in body, Content-Type: `text/plain`.
* `404`: Not found. Template doesn't exist.

## Data conversion

Data conversion would be initiated with:

```
POST [base]/{account}/convert/{sourceformat}/{template-name}
```

e.g.:

```
POST [base]/123/convert/hl7v2/bundle-fhir3.hbs
```

This would initiate a conversion of an HL7 v2 message using the template `bundle-fhir3.hbs` in account `123`. The actual HL7 v2 message would be provided in the payload.

Response:
* `200`: OK, message body would be converted and returned with appropriate content type, e.g. `application/json` for FHIR.
* `400`: Bad request. Errors during message conversion.
* `404`: Resource not found when converter is not found (e.g. POST [base]/123/convert/ccda/bundle-fhir3.hbs would fail if CCDA conversion is not implemented yet).
* `404`: Template not found if conversion request references a non-existent template (e.g. `bundle-fhir3.hbs` does not exist in current account)
* `404`: Partial not found. When a partial referenced in the template is not found.

## Template editing

To integrate with a live-editing experience either in Visual Studio Code or a browser based editor, we want the service to support on-the-fly template compilation and version. This would be accessed with:

```
POST [base]/{account}/convert/{format}
```

with a payload of:

```json
{
    "messageBase64": "eyxt6786...",
    "templateBase64": "yuoixgy6679...."
}
```

Where template and messages are supplied in base64 encoded form.

Response:
* `200`: OK, message body would be converted and returned with appropriate content type, e.g. `application/json` for FHIR.
* `400`: Bad request. Errors during message conversion.
* `404`: Resource not found when converter is not found (e.g. POST [base]/123/convert/ccda/bundle-fhir3.hbs would fail if CCDA conversion is not implemented yet).
* `404`: Partial not found. When a partial referenced in the template is not found.

**NOTE**: This endpoint *should* be throttled in production. Customers should only be using this for editing templates.

## Sample messages

To assist with editing, the service will have set of sample messages. A list of available messages can be obtained with:

```
GET [base]/{account}/samples/{format}
```

e.g.: 

```
GET [base]/123/samples/hl7v2
```

Response:
* `200`: OK, with a list of messages `['adt01.hl7, 'adt02.hl7', ....]`.
* `404`: Resource not found if message type (`{format}`) is not available.

A specific message would be retrived with:

```
GET [base]/{account}/samples/{format}/{message}
```

e.g.:

```
GET [base]/123/samples/hl7v2/adt01.hl7
```

Response:
* `200`: OK, message returned in body with appropriate content type, e.g. `text/plain` for HL7 v2.
* `404`: Resource not found if message type (`{format}`) is not available.
* `404`: Message not found if message type (`{message}`) is not found.

## Helper functions

Handlebars allows the use of "helper functions" in the templates. These helper functions must be registered with the handlebars instance prior to running the converter. In the MVP we will not allow customers to register their own helper functions, but we will provide a set of functions, which will be a combination of functions built into handlebars and some that we will create. An example from above would be:

```
{{addHypensBirthDate @this}}
```

where `addHypensBirthDate` is a helper function, which could look something like (javascript):

```javascript
function(birthDate) {
    bd = birthDate.toString();

    if (bd.indexOf('-') != -1)
    {
        return bd;
    }

    if (bd.length < 8)
    {
        return bd;
    }

    return bd.substring(0,4) + '-' + bd.substring(4,6) + '-' + bd.substring(6,8);
}
```

To assist with the editing experience (in VS Code or a browser based app), we will provide a way to list available helper functions:

```
GET [base]/{account}/helpers
```

Response:
* `200`: OK, with a list of helpers `['if', 'addHypensBirthDate', ....]`.

And there should be a way to get details about a function:

```
GET [base]/{account}/helpers/{helper}
```

e.g.:

```
GET [base]/123/helpers/addHypensBirthDate
```

Response:
* `200`: OK, with a :
    ```json
    {
        "function": "addHypensBirthDate",
        "description": "This function adds some hyphens to a birth date",
        "arguments": [
            {
                "position": 0,
                "type": "string",
                "description": "This is the birthdate in the form of 19810223"
            }
        ],
        "return": {
            "type": "string",
            "description": "The birthdate in the form 1981-02-23"
        }
    }
    ```
* `404`: Helper not found.

## Access control

The API will (optionally) use OAuth for access control. If enabled, a consumer of the API must present a valid access token.

In the MVP we will not attempt to use the access token to identify the specific converter account that is being used. A valid token will grant access to all converter accounts. See use of API management below for details on how accounts will be handled.

If the API is being accessed for an account that does not yet exits, one will be created and initialized (see below).

## API Management

To onboard multiple customers to the service and allow them to manage their own templates we need a system for managing and issuing accounts. In a final product, this should be managed through the portal, but for initial testing we can leverge [Azure API Management](https://azure.microsoft.com/en-us/services/api-management/). It provides tools for creating a developer portal (with API docs, instructions, etc), managing APIs and endpoints, and creating subscriptions to APIs, etc. It will also provide us with some ways to monitor the API usage, etc.

We can invite customers into the API management instance, let them subscribe to the conversion API and provide them with a way to obtain subscription keys.

When they consume the API, they will not provide a specific account identifier. We will write a policy in API management that, based on their subscription, determines which account they have and translates to appropriate, account prefixed URLs for the conversion API. The API management proxy will be responsible for obtaining a token for the backend API:

![converter with apim](ConversionApi/converter-api-mvp.png)

The use of API management is unlikely to be part of a product in the end, but it will allow us to leverage all the tools of APIM during the pilot phase instead of having to implement account management, etc.

## Account initialization

New accounts will be created on demand when a consumer tries to access an account that does not exist. In this MVP accounts are not security boundaries but simply ways to manage versions of templates. API management will handle access control and route to appropriate accounts (see above).

When a new account is accessed the API will:

1. Create a new folder in the blob storage for templates corresponding to the account name.

## Output (FHIR) validation

We could (optionally) add FHIR validation to the conversion API:

```
POST [base]/{account}/convert/{sourceformat}/{template-name}?validate=true
```

Would force FHIR validation of the resulting resource and result in an error if the generated resource is invalid. Similarly for the editing mode:

```
POST [base]/{account}/convert/{format}
```

with a payload of:

```json
{
    "messageBase64": "eyxt6786...",
    "templateBase64": "yuoixgy6679....",
    "validate": true
}
```

Validation would default to `false`.
 
## Cached templates and cache invalidation

When a consumer accesses the conversion API, the required template(s) will be pre-compiled and registered with handlebars. The use of a common set of standard start templates (prefixed with `$` as mentioned above) will mean (needs to be validated) than many partials will be shared among customers and a large number of customers will be able to be served on an instance of the service.

In the MVP phase, we anticipate just a few customers (< 10) but in a final production version there could be many customers and growth of the pre-compiled template cache would be a concern that may have to be mitigated. In the MVP, we will punt on this for now.

Some of the strategies to consider in production would be:

1. Using a hash of the templates to refer to partials when they are converted so that templates that are the same in several customers would always share the same pre-compiled version regardless of what name the customer may use to refer to them with. This would require maintaining some record (database) of the dependency graph of the templates so that when a partial template gets updated, templates that reference it will get rehashed forcing a recompile when they are used.
1. Keeping track of when the templates are used and invalidating (deleting from cache) ones that have not been accessed recently.
1. Routing specific accounts primarily to the same service instances to reduce the spread of all customers over all service instances.

# Test Strategy

Unit and integration tests will be built for the API. Deployment and E2E testing (with APIM) will also be included.

# Security

Security will primarily be handled in APIM as described above. Global access to the API backend will be secured with OAuth.

The API will not store any healthcare data. The templates *should* not contain PHI or PII and none of the messages that will be converted will be stored.

A security review should be conducted.