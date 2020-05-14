[[_TOC_]]

This page is currently in draft.

This solution makes no copy of anonymized data.
When an anonymized search request comes, the service re-identifies the search fields in the query, searches the database and anonymizes search results in runtime.

There are 2 kinds of possible implementations:
- Upstream re-dentification + downstream anonymization
- Pure downstream

# Upstream re-identification + downstream anonymization
In upstream, we re-identify original data from anonymized data.

In downstream, we only receive exact the same resources that the query asks for from the database.
We run anonymization on these resources and return them.

This requires more effort on upstream processing. Re-identification contains 2 steps:
- Get anonymization methods applied to search fields.
- Get original value with specific method.

## Get anonymization methods applied to search fields
To apply re-identification, first we need to know which methods are applied to search fields.
In this process, there is **a mapping from search parameters to its FHIR paths** and **a mapping from these FHIR paths to FHIR paths in anonymization configuration**.

Take following query as an example:
```
[base]/Patient?birthDate=1960
```
The first mapping can be done with SearchParameterDefinitionManager:
```
public SearchParameterInfo GetSearchParameter(string resourceType, string name)
public bool TryGetSearchParameter(string resourceType, string name, out SearchParameterInfo searchParameter)
```
We can get the definition of search parameter:
```json
{
  "fullUrl": "http://hl7.org/fhir/SearchParameter/individual-birthdate",
  "resource": {
    "name": "birthdate",
    "base": [
      "Patient",
      "Person",
      "RelatedPerson"
    ],
    "type": "date",
    "expression": "Patient.birthDate | Person.birthDate | RelatedPerson.birthDate",
    ...
  }
}
```
We get relative FHIR paths from "expression".
```
"Patient.birthDate | Person.birthDate | RelatedPerson.birthDate"
```
As the resource type in the query is Patient, we can find the target FHIR path:
```
"Patient.birthDate"
```
Once we get the target FHIR path, we can do the second mapping.
We find the first anonymization rule that covers the target FHIR path and get its method:
```json
{
  "fhirPathRules": [
    { "path": "Patient.birthDate", "method": "dateShift" },
    ...
  ]
}
```
If the target FHIR path starts with the FHIR path in anonymization rule, we consider it as matched because anonymization rule covers all children under this path.
Finding the first match is OK as former anonymization rules have higher priority than the latter ones.

## Get original value with specific method

### Redact (complete)
According to the spec, searching completely redacted fields is not available.
We can **replace this condition with a new one that will never be satisfied**.

Take following query as an example:
```
[base]/Patient?name=kirk
```

We can replace the original name with a GUID:
```
[base]/Patient?name=1828265f-ed20-4cfc-9c64-381d6672f107
```

No result will be returned by this query.

### Redact (partial)
Date, dateTime, instant and postal code data could be partially redacted according to Safe Harbor method.

- Example 1, anonymize 2002-01-03 to 2002. Year could keep as indicative age is not over 89.
- Example 2, anonymize 98052 to 98000, first 3 digits could keep as this postal code is not restricted.

For postal code data, one problem is that when searching with address, we do not know whether the value comes from postal code or other fields.
If it comes from postal code, we should search with prefix (980).
If not, we should search with entire value.
```
[base]/Patient?address=98000
```

If we want to enable it, we need to **make an assumption of the format of postal code**, like a string made up of 5 digits. So we can distinguish postal code from other data.

For date, dateTime and instant data, search is available with value remained (2002).
Data that fit in the range are all returned.

### DateShift
According to the spec, date-shifted fields should be able for query using the shifted values.

To achieve this, **the shifting amount should be the same for the whole endpoint.**
Date shift by resource ids are not applicable, because the shifting amount cannot be pre-calculated until we gets the exact resource.

```json
{
  "dateShiftAmount": 10
}
```

Note that time is redacted to zero for dateTime and instant data.
Data that matches the date but of different times will all be returned.
**If we need to do exact match, we can keep time in anonymized data.**

### CryptoHash
CryptoHash is used for resource ids in case they contain identifiers.
Since resource id in FHIR Server is replaced with GUID, we do not need to apply cryptoHash again.

(If we want to support cryptoHash, when creating an anonymized endpoint, we need to run cryptoHash according to the configuration and save the results in database.
So when a request with a crypto-hashed value comes, we can look up the database and find its original value.
Looking up the database frequently might be expensive.)

## Special cases
### Address and HumanName
According to FHIR Search spec, search of Address and HumanName cover all string elements in them.
In FHIR Server, a patient's postal code is stored in both "address" and "address-postalcode":

```json
{
    "p": "address",
    "s": "Anycity",
    "n_s": "ANYCITY"
},
{
    "p": "address",
    "s": "Anydistrict",
    "n_s": "ANYDISTRICT"
},
{
    "p": "address",
    "s": "123 Main Street",
    "n_s": "123 MAIN STREET"
},
{
    "p": "address",
    "s": "12345",
    "n_s": "12345"
},
{
    "p": "address",
    "s": "CA",
    "n_s": "CA"
},
{
    "p": "address",
    "s": "123 Main Street Anycity, Anydistrict, CA 12345",
    "n_s": "123 MAIN STREET ANYCITY, ANYDISTRICT, CA 12345"
},
{
    "p": "address-city",
    "s": "Anycity",
    "n_s": "ANYCITY"
},
{
    "p": "address-postalcode",
    "s": "12345",
    "n_s": "12345"
},
{
    "p": "address-state",
    "s": "CA",
    "n_s": "CA"
},
{
    "p": "address-use",
    "s": "http://hl7.org/fhir/address-use",
    "c": "home"
},
```
If Patient.address.postalcode is configured "redact":
```json
{
  "fhirPathRules": [
    { "path": "Patient.address.postalCode", "method": "redact" },
    ...
  ],
  "parameters": {
    "enablePartialZipCodesForRedact": false
  }
}
```
when the user searches:
```
[base]/Patient?address-postalcode=12345
```
by above steps we get the target FHIR path "Patient.address.postalCode" and we know the method is "redact". But when the user searches:
```
[base]/Patient?address=12345
```
we get the target FHIR path "Patient.address" and there's no anonymization rule matched.

If we flatten Address and HumanName to its children elements, we can solve some of this problem.
```
[base]/Patient?address-postalcode=12345,address-city=12345,address-state=12345,address-country=12345
```

But for "Patient.address.text", it is searchable with search parameter "address", but it does not has an independent search parameter.
We can not distinguish it from other values stored in "address" field.

### Multiple resource types
Search can apply to multiple or all resource types.
But when matching the search parameters to FHIR paths in anonymization configuration, we need to know the exact resource types.

When the user searches:
```
[base]?birthdate=1980-12-15&_type=Practitioner,Patient,Person
```
with following anonymization configuration:
```json
{
  fhirPathRules: [
    { "path": "Patient.birthDate", "method": "dateShift" },
    { "path": "Practitioner.birthDate", "method": "keep" },
    { "path": "Person.birthDate", "method": "redact" },
    ...
  ],
  "parameters": {
    "dateShiftAmount": 10,
    "enablePartialDatesForRedact": false,
  }
}
```
we need to search Patient with 1980-12-05, Practitioner with 1980-12-15 and Person with a value that will never be satisfied.

### Composite search
If the type of the search parameter is composite, instead of looking directly into its expression, we should iterate its components and find the expressions of its components instead.

### Modifiers
For ":text" modifier with token, we need to map the FHIR paths to the text part - either CodeableConcept.text, Coding.display, or Identifier.type.text.

For ":missing" modifier, we are not sure whether this could be supported.
Searching for "name:missing=true" will return all the resources that don't have a value for the name parameter.
If the search parameter has modifier ":missing=true" and its relative FHIR path is redacted, we need to remove this condition from the query, because every anonymized resource satisfies this condition.
If it's relative FHIR path is partially redacted or date-shifted, we are not sure how to handle them yet.

### Prefix
Since prefix can change the meaning of the query completely, it needs to be processed very carefully.

Prefix can appear before multiple data types, like date, number and quantity.

For example, searching with prefix "ne" before a date returns dates that does not meet this value:
```
[base]/Patient?birthdate=ne1998-01-01
```
This changes the meaning of the query completely.

### _text, _content, _query parameters
The _text and _content parameters search on the narrative of the resource and the entire content of the resource respectively.
The _query parameter names a custom search profile that describes a specific search operation.
_text, _content, _query have empty expression in definition.
We are not able to map them with specific FHIR paths in anonymization configuration.
These search parameters are not supported.

## Pros and cons
Pros:
- It is a safe operation as no extra data is fetched from the database.

Cons:
- The implementation of re-identification is complicated.
Special cases need to be handled properly.
- Only basic FHIR paths are supported in anonymization configuration.

# Pure downstream
In upstream, we do not apply any re-identification.
Instead, we search by both anonymized values and original values.
- If the query contains date, we search by both original date and shifted date.
- If the query contains postal code, we search by the first 3 digits, if enablePartialZipCodesForRedact is true and the value in the query is not in restricted areas.

In downstream, because we search with more candidate values, we get more results than the query asks.
First we run anonymization on search results.
For every anonymized search result, we iterate the search parameters and for every search parameter, we get its relative FHIR path from definition.
We fetch the value of this FHIR path in anonymized result and see if it exists.
If not, it means this result is retrieved by some field that is anonymized. This result should be removed.

This requires more processing effort on downstream.

## Special cases
- Modifier ":missing" is not supported.
- Prefix should be handled properly.

## Pros and cons
Pros:
- All FHIR paths that anonymization configuration supports could be used.

Cons:
- We fetch more results than the query asks for from the database.
- This requires more computation on extra resources. We need to evaluate the time performance.
