Customers would like to use the Azure API for FHIR in conjunction with Analytics and ML. The FHIR API may not always be suitable for data exploration and analytics and customers will want to extract data into Azure Blob Storage, Azure Data Lake Storage, Azure Data Explorer, etc. It should be possible to query the FHIR server for a change feed and get all changes (create, update, delete) in a certain time frame. Customer would use this change feed in Azure Functions and Logic Apps to process changes.

[[_TOC_]]

# High-Level Design

Currently, a customer can do:

```
GET //fhirserver/_history?_since=2019-04-19T15:54:00
```

But a user would want to bracket the search results:

```
GET //fhirserver/_history?_since=2019-04-19T15:54:00&_before=2019-04-19T15:56:00
```

And also query how many results are available:

```
GET //fhirserver/_history?_since=2019-04-19T15:54:00&_before=2019-04-19T15:56:00&_summary=count
```

This would allow breaking a bracket into smaller ones for parallel processing.

The current result looks like:

```json
{
    "resourceType": "Bundle",
    "id": "60c665ac6260a4428bd36fafedf2418b",
    "meta": {
        "lastUpdated": "2019-04-19T16:09:00.9237528+00:00"
    },
    "type": "history",
    "link": [
        {
            "relation": "self",
            "url": "https://mihanch1.azurehealthcareapis.com/_history?_since=2019-04-19T15%3A54%3A00&_before=2019-04-19T15%3A55%3A00"
        }
    ],
    "entry": [
        {
            "fullUrl": "https://mihanch1.azurehealthcareapis.com/Patient/18a2ac75-3b9f-486c-882b-aff34d436164/_history/2",
            "resource": {
                "resourceType": "Patient",
                "id": "18a2ac75-3b9f-486c-882b-aff34d436164",
                "meta": {
                    "versionId": "2",
                    "lastUpdated": "2019-04-19T15:55:23.8325521+00:00"
                }
            },
            "request": {
                "method": "DELETE",
                "url": "https://mihanch1.azurehealthcareapis.com/Patient/18a2ac75-3b9f-486c-882b-aff34d436164"
            },
            "response": {
                "etag": "W/\"2\"",
                "lastModified": "2019-04-19T15:55:23.8325521+00:00"
            }
        },
        {
            "fullUrl": "https://mihanch1.azurehealthcareapis.com/Patient/18a2ac75-3b9f-486c-882b-aff34d436164/_history/1",
            "resource": {
                "resourceType": "Patient",
                "id": "18a2ac75-3b9f-486c-882b-aff34d436164",
                "meta": {
                    "versionId": "1",
                    "lastUpdated": "2019-04-19T15:54:58.940311+00:00"
                },
                "active": true,
                "name": [
                    {
                        "use": "official",
                        "family": "Kirk",
                        "given": [
                            "James",
                            "Tiberious"
                        ]
                    },
                    {
                        "use": "usual",
                        "given": [
                            "Jim"
                        ]
                    }
                ],
                "gender": "male",
                "birthDate": "1960-12-25"
            },
            "request": {
                "method": "POST",
                "url": "https://mihanch1.azurehealthcareapis.com/Patient"
            },
            "response": {
                "etag": "W/\"1\"",
                "lastModified": "2019-04-19T15:54:58.940311+00:00"
            }
        }
    ]
}
```

As seen the results include records that have been deleted, but if you do a `hardDelete`:

```
DELETE //fhirserver/Patient/18a2ac75-3b9f-486c-882b-aff34d436164??hardDelete=true
```

The same `_history` search comes up empty:

```json
{
    "resourceType": "Bundle",
    "id": "fe02189da821a14389b475c33f8c3da8",
    "meta": {
        "lastUpdated": "2019-04-19T16:18:39.4636476+00:00"
    },
    "type": "history",
    "link": [
        {
            "relation": "self",
            "url": "https://mihanch1.azurehealthcareapis.com/_history?_since=2019-04-19T15%3A54%3A00"
        }
    ]
}
```

Some customers will want to see the HardDeletes as well. We will have to discuss if that should be available. 

## Phased approach

This work can be split in two:

1. Enabling effective use of `_history` for change feed:
    1. Enabling a `_before` parameter to allow bracketing of `_history` searches.
    2. Enable `_summary=count` for `_history`
1. Enable a way to get hard deleted resources.

# Test Strategy

Testing should include:

1. No records fall between two time points, i.e. the given the following two searches:
    * `/?_since=2019-04-18T06:00:00&_before=2019-04-18T08:00:00`
    * `/?_since=2019-04-18T08:00:00&_before=le2019-04-18T10:00:00`

    There should be no additional records around the `2019-04-18T08:00:00` that are missed. There should be no records that will be returned in both queries. 
1. Shadow records are produced for hard deleted records.

# Security

RBAC rules should apply since this is based on simple search. The issue of hard deletes would need to be explored a bit. Are there compliance problems with providing shadow records for deleted records? One possible solution to this would be to have an option to hash all `id`s when pulling the change feed.

# Other

This feature could help us to defer the implementation of `Subscription` until we have a good way to do that.
