Customers would like to use the Azure API for FHIR in conjunction with Analytics and ML. The FHIR API may not always be suitable for data exploration and analytics and customers will want to extract data into Azure Blob Storage, Azure Data Lake Storage, Azure Data Explorer, etc. It should be possible to query the FHIR server for a change feed and get all changes (create, update, delete) since a certain time (either defined as an actual datetime or possible a sequence number). Customer would use this change feed in Azure Functions and Logic Apps to process changes.

[[_TOC_]]

# High-Level Design

A customer would query for changes with a query like this

```
GET //fhirserver/?_lastUpdated=ge2019-04-18T06:00:00&_lastUpdated=le2019-04-18T08:00:00&_includeDeletes
```

And get all the changes in that two hour block. Our service currently has that capability except for the `_includeDeletes` parameter. Adding this to the search would include shadow records for resources that have been delete. Specifically, the result could look like:


```json
{
    "resourceType": "Bundle",
    "id": "642be6b710fda04fad9878ab8cffad6c",
    "meta": {
        "lastUpdated": "2019-04-18T16:22:55.1685063+00:00"
    },
    "type": "searchset",
    "link": [
        {
            "relation": "next",
            "url": "https://fhirsampdev.azurehealthcareapis.com/?_lastUpdated=ge2019-04-18T06%3A00%3A00&_lastUpdated=le2019-04-18T08%3A00%3A00&ct=%7B%22token%22%3A%22%2BRID%3Ad89eAIFEhXEPAAAAAAAAAA%3D%3D%23RT%3A1%23TRC%3A10%23FPC%3AAgEAAQA0AA%2BAIkB%2FAAQAgYJHgSaAGoAtgOuAZoCPgPGJhYQjoSGB04A7gU%2BABYNUgIeAvoBNgE6ATYABBAAWgnun%22,%22range%22%3A%7B%22min%22%3A%22%22,%22max%22%3A%22FF%22%7D%7D"
        },
        {
            "relation": "self",
            "url": "https://fhirsampdev.azurehealthcareapis.com/?_lastUpdated=ge2019-04-18T06%3A00%3A00&_lastUpdated=le2019-04-18T08%3A00%3A00"
        }
    ],
    "entry": [
        {
            "fullUrl": "https://fhirsampdev.azurehealthcareapis.com/Organization/e02b9018-404d-48ca-8204-9cefa434a976",
            "resource": {
                "resourceType": "Organization",
                "id": "e02b9018-404d-48ca-8204-9cefa434a976",
                "meta": {
                    "versionId": "3",
                    "lastUpdated": "2019-04-18T07:27:55.7179856+00:00",
                    "profile": [
                        "http://standardhealthrecord.org/fhir/StructureDefinition/shr-entity-Organization"
                    ]
                },
                "identifier": [
                    {
                        "system": "https://github.com/synthetichealth/synthea",
                        "value": "2428b347-3bb1-4a12-8963-3dd72817136e"
                    },
                    {
                        "system": "urn:ietf:rfc:3986",
                        "value": "2428b347-3bb1-4a12-8963-3dd72817136e"
                    }
                ],
                "type": [
                    {
                        "coding": [
                            {
                                "system": "Healthcare Provider",
                                "code": "prov",
                                "display": "Healthcare Provider"
                            }
                        ],
                        "text": "Healthcare Provider"
                    }
                ],
                "name": "SHRINERS' HOSPITAL FOR CHILDREN (THE)",
                "telecom": [
                    {
                        "system": "phone",
                        "value": "4137872000"
                    }
                ],
                "address": [
                    {
                        "line": [
                            "516 CAREW STREET"
                        ],
                        "city": "SPRINGFIELD",
                        "state": "MA",
                        "postalCode": "01104",
                        "country": "US"
                    }
                ],
                "contact": [
                    {
                        "name": {
                            "text": "Synthetic Provider"
                        }
                    }
                ]
            },
            "search": {
                "mode": "match"
            }
        },
        {
            "fullUrl": "https://fhirsampdev.azurehealthcareapis.com/Claim/e9314b9a-14e2-45a9-8597-1d4f99943580",
            "resource": {
                "resourceType": "Claim",
                "id": "e9314b9a-14e2-45a9-8597-1d4f99943580",
                "meta": {
                    "versionId": "3",
                    "lastUpdated": "2019-04-18T07:27:55.78036+00:00",
                    "resourceDeletedTime": "2019-04-18T07:27:55.78036+00:00"
                }
            }
        },
        {
            //Another resource
        }
    ]
}
```

The shadow records for the deleted resources should include any hard deleted resources. There may be some limitations, specifically that we wil only guarantee the hard deleted records if the request is made within some time of the hard delete.

# Test Strategy

Testing should include:

1. No records fall between two time points, i.e. the given the following two searches:
    * `/?_lastUpdated=gt2019-04-18T06:00:00&_lastUpdated=le2019-04-18T08:00:00`
    * `/?_lastUpdated=gt2019-04-18T08:00:00&_lastUpdated=le2019-04-18T10:00:00`

    There should be no additional records around the `2019-04-18T08:00:00` that are missed.
1. Shadow records are produced for deleted records.
1. Shadow records are produced for hard deleted records.

# Security

RBAC rules should apply since this is based on simple search. The issue of hard deletes would need to be explored a bit. Are there compliance problems with providing shadow records for deleted records? One possible solution to this would be to have an option to hash all `id`s when pulling the change feed.

# Other

This feature could help us to defer the implementation of `Subscription` until we have a good way to do that.
