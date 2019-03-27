FHIR is very extensible and customers that make use of extensions in FHIR resources will want to make some of those extensions searchable. As an example, customers using the US Core (http://hl7.org/fhir/us/core/) profiles may want to be able to search fields such as `race` or `ethnicity`.

The FHIR standard provides a framework for defining new search parameters and we should provide customers with a mechanism add new search parameters. Note that adding a new search parameter is relatively trivial, but re-indexing will be needed and we need to provide customers with a way to start re-indexing and monitor if it is done.

[[_TOC_]]

# Adding a new `SearchParameter` in the FHIR server

To set the stage for the design, we will first have a look at what it takes to add a new SearchParameter to the existing FHIR server. It is fairly trivial. Suppose we want to be able to search `race` on `Patient` resources that have the US core extension for race. 

The search parameters are defined in `src\Microsoft.Health.Fhir.Core\Features\Definition\search-parameters.json`. We can add a section with something like:

```json
    {
      "fullUrl": "http://hl7.org/fhir/SearchParameter/Patient-race",
      "resource": {
        "resourceType": "SearchParameter",
        "id": "Patient-race",
        "url": "http://hl7.org/fhir/SearchParameter/Patient-race",
        "name": "race",
        "status": "draft",
        "experimental": false,
        "date": "2017-04-19T07:44:43+10:00",
        "code": "race",
        "base": [
          "Patient"
        ],
        "type": "token",
        "description": "Patient race",
        "expression": "Patient.extension.where(url = 'http://hl7.org/fhir/us/core/StructureDefinition/us-core-race').first().extension.where(url = 'ombCategory').value",
        "xpath": "f:Patient/f:extension[@url='http://hl7.org/fhir/us/core/StructureDefinition/us-core-race']/f:extension[@url = 'ombCategory']",
        "xpathUsage": "normal"
      }
```

Additionally, we need to add something like:

```json
    {
        "name": "race",
        "definition": "http://hl7.org/fhir/SearchParameter/Patient-race",
        "type": "token",
        "documentation": "Patient race"
    },

```

To both `src\Microsoft.Health.Fhir.Core\Features\Conformance\AllCapabilities.json` and `src\Microsoft.Health.Fhir.Core\Features\Conformance\DefaultCapabilities.json` to make the search parameter show up in the capability statement.

After adding this search parameter, if we upload a Synthea patients (they have the US core race extension), we see something like this in the database:

```json
{
    "id": "a8a81f34-d235-42a6-9b95-88601ea60089",
    "isSystem": false,
    "version": "1",
    "searchIndices": [
        {
            "p": "_id",
            "c": "a8a81f34-d235-42a6-9b95-88601ea60089"
        },
        {
            "p": "_lastUpdated",
            "st": "2019-03-24T15:16:07.1136876+00:00",
            "et": "2019-03-24T15:16:07.1136876+00:00"
        },
        {
            "p": "active",
            "s": "http://hl7.org/fhir/special-values",
            "c": "false"
        },
        {
            "p": "race",
            "s": "urn:oid:2.16.840.1.113883.6.238",
            "c": "2106-3",
            "n_t": "BLACK"
        },
        {
            "p": "address",
            "s": "Worcester",
            "n_s": "WORCESTER"
        },
        {
            "p": "address",
            "s": "US",
            "n_s": "US"
        },
        {
            "p": "address",
            "s": "274 Lesch Wynd Suite 57",
            "n_s": "274 LESCH WYND SUITE 57"
        },
        {
            "p": "address",
            "s": "01545",
            "n_s": "01545"
        },
        {
            "p": "address",
            "s": "Massachusetts",
            "n_s": "MASSACHUSETTS"
        },
        {
            "p": "address-city",
            "s": "Worcester",
            "n_s": "WORCESTER"
        },
        {
            "p": "address-country",
            "s": "US",
            "n_s": "US"
        },
        {
            "p": "address-postalcode",
            "s": "01545",
            "n_s": "01545"
        },
        {
            "p": "address-state",
            "s": "Massachusetts",
            "n_s": "MASSACHUSETTS"
        },
        {
            "p": "birthdate",
            "st": "1961-09-02T00:00:00.0000000+00:00",
            "et": "1961-09-02T23:59:59.9999999+00:00"
        },
        {
            "p": "deceased",
            "s": "http://hl7.org/fhir/special-values",
            "c": "false"
        },
        {
            "p": "family",
            "s": "Emard19",
            "n_s": "EMARD19"
        },
        {
            "p": "family",
            "s": "Muller251",
            "n_s": "MULLER251"
        },
        {
            "p": "gender",
            "s": "http://hl7.org/fhir/administrative-gender",
            "c": "female"
        },
        {
            "p": "given",
            "s": "Antonia30",
            "n_s": "ANTONIA30"
        },
        {
            "p": "identifier",
            "s": "https://github.com/synthetichealth/synthea",
            "c": "c61a7337-d81f-4c39-9e4b-37c3ee4fb66f"
        },
        {
            "p": "identifier",
            "s": "http://hospital.smarthealthit.org",
            "c": "c61a7337-d81f-4c39-9e4b-37c3ee4fb66f",
            "n_t": "MEDICAL RECORD NUMBER"
        },
        {
            "p": "identifier",
            "s": "http://hl7.org/fhir/sid/us-ssn",
            "c": "999-17-3672",
            "n_t": "SOCIAL SECURITY NUMBER"
        },
        {
            "p": "identifier",
            "s": "urn:oid:2.16.840.1.113883.4.3.25",
            "c": "S99925974",
            "n_t": "DRIVER'S LICENSE"
        },
        {
            "p": "identifier",
            "s": "http://standardhealthrecord.org/fhir/StructureDefinition/passportNumber",
            "c": "X75909454X",
            "n_t": "PASSPORT NUMBER"
        },
        {
            "p": "language",
            "n_t": "ENGLISH"
        },
        {
            "p": "language",
            "s": "urn:ietf:bcp:47",
            "c": "en-US",
            "n_t": "ENGLISH"
        },
        {
            "p": "name",
            "s": "Antonia30",
            "n_s": "ANTONIA30"
        },
        {
            "p": "name",
            "s": "Emard19",
            "n_s": "EMARD19"
        },
        {
            "p": "name",
            "s": "Mrs.",
            "n_s": "MRS."
        },
        {
            "p": "name",
            "s": "Antonia30",
            "n_s": "ANTONIA30"
        },
        {
            "p": "name",
            "s": "Muller251",
            "n_s": "MULLER251"
        },
        {
            "p": "name",
            "s": "Mrs.",
            "n_s": "MRS."
        },
        {
            "p": "phone",
            "s": "home",
            "c": "555-787-6923"
        },
        {
            "p": "phonetic",
            "s": "Antonia30",
            "n_s": "ANTONIA30"
        },
        {
            "p": "phonetic",
            "s": "Emard19",
            "n_s": "EMARD19"
        },
        {
            "p": "phonetic",
            "s": "Mrs.",
            "n_s": "MRS."
        },
        {
            "p": "phonetic",
            "s": "Antonia30",
            "n_s": "ANTONIA30"
        },
        {
            "p": "phonetic",
            "s": "Muller251",
            "n_s": "MULLER251"
        },
        {
            "p": "phonetic",
            "s": "Mrs.",
            "n_s": "MRS."
        },
        {
            "p": "telecom",
            "s": "home",
            "c": "555-787-6923"
        }
    ],
    "partitionKey": "Patient_a8a81f34-d235-42a6-9b95-88601ea60089",
    "lastModified": "2019-03-24T15:16:07.1136876+00:00",
    "rawResource": {
        "data": "{\"resourceType\":\"Patient\",\"id\":\"a8a81f34-d235-42a6-9b95-88601ea60089\",\"text\":{\"status\":\"generated\",\"div\":\"<div xmlns=\\\"http://www.w3.org/1999/xhtml\\\">Generated by <a href=\\\"https://github.com/synthetichealth/synthea\\\">Synthea</a>.Version identifier: v2.2.0-56-g113d8a2d\\n .   Person seed: 5590707601642823062  Population seed: 1546890380968</div>\"},\"extension\":[{\"extension\":[{\"url\":\"ombCategory\",\"valueCoding\":{\"system\":\"urn:oid:2.16.840.1.113883.6.238\",\"code\":\"2106-3\",\"display\":\"Black\"}},{\"url\":\"text\",\"valueString\":\"Black\"}],\"url\":\"http://hl7.org/fhir/us/core/StructureDefinition/us-core-race\"},{\"extension\":[{\"url\":\"ombCategory\",\"valueCoding\":{\"system\":\"urn:oid:2.16.840.1.113883.6.238\",\"code\":\"2186-5\",\"display\":\"Not Hispanic or Latino\"}},{\"url\":\"text\",\"valueString\":\"Not Hispanic or Latino\"}],\"url\":\"http://hl7.org/fhir/us/core/StructureDefinition/us-core-ethnicity\"},{\"url\":\"http://hl7.org/fhir/StructureDefinition/patient-mothersMaidenName\",\"valueString\":\"Matthew562 Will178\"},{\"url\":\"http://hl7.org/fhir/us/core/StructureDefinition/us-core-birthsex\",\"valueCode\":\"F\"},{\"url\":\"http://hl7.org/fhir/StructureDefinition/birthPlace\",\"valueAddress\":{\"city\":\"Boston\",\"state\":\"Massachusetts\",\"country\":\"US\"}},{\"url\":\"http://synthetichealth.github.io/synthea/disability-adjusted-life-years\",\"valueDecimal\":0.003979369946003509},{\"url\":\"http://synthetichealth.github.io/synthea/quality-adjusted-life-years\",\"valueDecimal\":56.996020630054}],\"identifier\":[{\"system\":\"https://github.com/synthetichealth/synthea\",\"value\":\"c61a7337-d81f-4c39-9e4b-37c3ee4fb66f\"},{\"type\":{\"coding\":[{\"system\":\"http://hl7.org/fhir/v2/0203\",\"code\":\"MR\",\"display\":\"Medical Record Number\"}],\"text\":\"Medical Record Number\"},\"system\":\"http://hospital.smarthealthit.org\",\"value\":\"c61a7337-d81f-4c39-9e4b-37c3ee4fb66f\"},{\"type\":{\"coding\":[{\"system\":\"http://hl7.org/fhir/identifier-type\",\"code\":\"SB\",\"display\":\"Social Security Number\"}],\"text\":\"Social Security Number\"},\"system\":\"http://hl7.org/fhir/sid/us-ssn\",\"value\":\"999-17-3672\"},{\"type\":{\"coding\":[{\"system\":\"http://hl7.org/fhir/v2/0203\",\"code\":\"DL\",\"display\":\"Driver's License\"}],\"text\":\"Driver's License\"},\"system\":\"urn:oid:2.16.840.1.113883.4.3.25\",\"value\":\"S99925974\"},{\"type\":{\"coding\":[{\"system\":\"http://hl7.org/fhir/v2/0203\",\"code\":\"PPN\",\"display\":\"Passport Number\"}],\"text\":\"Passport Number\"},\"system\":\"http://standardhealthrecord.org/fhir/StructureDefinition/passportNumber\",\"value\":\"X75909454X\"}],\"active\":false,\"name\":[{\"use\":\"official\",\"family\":\"Emard19\",\"given\":[\"Antonia30\"],\"prefix\":[\"Mrs.\"]},{\"use\":\"maiden\",\"family\":\"Muller251\",\"given\":[\"Antonia30\"],\"prefix\":[\"Mrs.\"]}],\"telecom\":[{\"system\":\"phone\",\"value\":\"555-787-6923\",\"use\":\"home\"}],\"gender\":\"female\",\"birthDate\":\"1961-09-02\",\"address\":[{\"extension\":[{\"extension\":[{\"url\":\"latitude\",\"valueDecimal\":42.269478},{\"url\":\"longitude\",\"valueDecimal\":-71.807783}],\"url\":\"http://hl7.org/fhir/StructureDefinition/geolocation\"}],\"line\":[\"274 Lesch Wynd Suite 57\"],\"city\":\"Worcester\",\"state\":\"Massachusetts\",\"postalCode\":\"01545\",\"country\":\"US\"}],\"maritalStatus\":{\"coding\":[{\"system\":\"http://hl7.org/fhir/v3/MaritalStatus\",\"code\":\"M\",\"display\":\"M\"}],\"text\":\"M\"},\"multipleBirthBoolean\":false,\"communication\":[{\"language\":{\"coding\":[{\"system\":\"urn:ietf:bcp:47\",\"code\":\"en-US\",\"display\":\"English\"}],\"text\":\"English\"}}]}",
        "format": "Json"
    },
    "request": {
        "url": "https://localhost:44348/Patient",
        "method": "POST"
    },
    "isDeleted": false,
    "resourceId": "a8a81f34-d235-42a6-9b95-88601ea60089",
    "resourceTypeName": "Patient",
    "isHistory": false,
    "lastModifiedClaims": [],
    "compartmentIndices": {},
    "_rid": "SBY1AJIVDN1AAAAAAAAAAA==",
    "_self": "dbs/SBY1AA==/colls/SBY1AJIVDN0=/docs/SBY1AJIVDN1AAAAAAAAAAA==/",
    "_etag": "\"5601fc08-0000-0800-0000-5c979f380000\"",
    "_attachments": "attachments/",
    "_ts": 1553440568
}
```

Notice how `race` has been extracted as a search parameter and we can now do searches like:

```
GET https://fhirserver/Patient?race=2106-3
```

or

```
GET https://fhirserver/Patient?race:text=black
```

So we have all the core capabilities for providing this for customers. The work is mostly related to making this operational. 

# High-Level Design

Describe the high-level design -- enough detail that code reviewers and stakeholders are not surprised.

# Test Strategy

Describe the test strategy.

# Security

Describe any special security implications or security testing needed.

# Other

Describe any impact to localization, globalization, deployment, back-compat, SOPs, ISMS, etc.

