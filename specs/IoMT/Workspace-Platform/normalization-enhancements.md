# Support independent provisioning of IoT Connector AKS Resources

[[_TOC_]]

# Background
The purpose of this document is to detail changes to support the normalization enhancement requests of Feature [Iot Connector Normalization Improvements](https://microsofthealth.visualstudio.com/Health/_workitems/edit/79488). 
The change surround the following topics:

- Provide end users the ability to perform functions on incoming EventData. This can be used to derive calculated values or convert incoming data in some way
- Allow for incoming data to be projected and still have access to data that exists within other scopes.

As these features are closely related, they will be contained within this document. The document will be expanded as the features get flushed out.

# Calculated Functions
Users sometimes submit data within their events that require additional processing to be useful. Take the following for example:

```json
{
  "preflang": "en_EN",
  "birthdate": "12345678",
  "gender": 1,
  "shortname": "ABC",
  "measures": [
    {
      "value": 180,
      "timestamp": 1622655433,
      "unit": -2,
      "type": 4
    },
    {
      "value": 8000,
      "timestamp": 1622655433,
      "unit": -2,
      "type": 1
    }
  ],
}
```
The individual measures contain components which represent a single value. In the first meaure:

- Type 1 corresponds with __Height__
- Unit -2 indicates the position of the decimal point
- Value 8000 indicates the unformated value.

A customer would apply a calculation on this to achieve the value: **80.00kg**

## Specifying Calculations
Our processing pipeline has the concept of [IContentTemplate](https://github.com/microsoft/iomt-fhir/blob/master/src/lib/Microsoft.Health.Fhir.Ingest.Template/IContentTemplate.cs) which extract measurements from incoming Json event data. Our basic implementation of this is [JsonPathContentTemplate](https://github.com/microsoft/iomt-fhir/blob/master/src/lib/Microsoft.Health.Fhir.Ingest.Template/JsonPathContentTemplate.cs), which allows the basic extraction of values via JsonPath. Additionally, this class enforces that certain required values are extracted, such as:
- PatientId
- DeviceId
- Timestamp

Keeping with this design, a new **ExpressionContentTemplate** will be created which will replace the use of JsonPath with that of a more robust expression language. As this will implement [IContentTemplate](https://github.com/microsoft/iomt-fhir/blob/master/src/lib/Microsoft.Health.Fhir.Ingest.Template/IContentTemplate.cs) a user can use this in places where they would have used an instance of JsonPathContentTemplate. 

The choice of using Liquid or JmesSpath will be evaluated in a subsequent section.

Current JsonPathContentTemplate Snippet
```json
{
  "templateType": "CollectionContent",
  "template": [
      {
          "templateType": "IotJsonPathContent",
          "template": {
              "typeName": "heartrate",
              "typeMatchExpression": "$..[?(@Body.HeartRate)]",
              "patientIdExpression": "$.SystemProperties.iothub-connection-device-id",
              "values": [
                  {
                      "required": "true",
                      "valueExpression": "$.Body.HeartRate",
                      "valueName": "hr"
                  }
              ]
          }
      }
    ]
}
```

Proposed JsonPathContentTemplate Snippet
```json
{
  "templateType": "CollectionContent",
  "template": [
      {
          "templateType": "IotJsonPathContent",
          "template": {
              "typeName": "heartrate",
              "typeMatchExpression": "$..[?(@Body.HeartRate)]",
              "patientIdExpression": "$.SystemProperties.iothub-connection-device-id",
              "values": [
                  {
                      "required": "true",
                      "valueExpression": "$.Body.HeartRate",
                      "valueName": "hr"
                  }
              ]
          }
      },
      {
          "templateType": "ExpressionContentTemplate",
          "template": {
              "typeName": "rangeofmotion",
              "typeMatchExpression": "$..[?(@Body.RangeOfMotion)]",
              "patientIdExpression": "$.SystemProperties.iothub-connection-device-id",
              "values": [
                  {
                      "required": "true",
                      "valueExpression": {
                          "expressionLanguage" : "Liquid",
                          "expression" : "
                          {%- assign heightData   = measures | where: type, 4 | first -%}
                          {%- assign height       = heightData.value | downcase -%}
                          {%- assign heightLength = height | size -%}
                          {%- assign insertPos    = heightLength | plus: heightData.unit -%}
                          {%- assign prefix       = height | slice: 0, insertPos  -%}
                          {%- assign suffix       = height | slice: insertPos, heightLength -%}
                          {{- prefix | append: ',' | append: suffix | | append: 'm' -}}"
                      },
                      "valueName": "height"
                  }
              ]
          }
      },
  ]
}
```

### Specifying Expressions
Each 'expression' on the new ExpressionContentTemplate may be either a Json String or Object. If a user supplies a String, we will evaluate this as basic JsonPath. If it's an object, it must be in the form specified below to indicate its expression language. The idea here is that some expressions may be better represented in JsonPath versus the templateing language. 

```json
{
    "templateType": "ExpressionContentTemplate",
    "template": {
        "typeName": "rangeofmotion",
        "typeMatchExpression": "$..[?(@Body.RangeOfMotion)]",
        ...
    }
},
```

would be equivalent to:

```json
{
    "templateType": "ExpressionContentTemplate",
    "template": {
        "typeName": "rangeofmotion",
        "typeMatchExpression": {
          "language" : "JsonPath",
          "expression": "$..[?(@Body.RangeOfMotion)]"
        }
    }
},
```

### Expression Evaluation
The expression will be evaluated inside of the normalization process. This will be achieved via a DotNet implemenation of the expression language framework. 

Once evaluated it is expected that the result will be a valid Json object. The type of object returned must correspond with the type of expression requested.

| Expression Type     | Expected Json Value                     |
|---------------------|-----------------------------------------|
| typeMatch           | Enumerable of Json Objects (i.e JToken) |
| patientId           | String                                  |
| encounterId         | String                                  |
| deviceId            | String                                  |
| correlationId       | String                                  |
| timestampExpression | Date                                    |
| valueExpression     | String / Number / Boolean               |

The evaluation pipeline will continue to work as it is done today. This includes the following steps as defined [here](https://github.com/microsoft/iomt-fhir/blob/master/docs/Configuration.md#mapping-with-json-path):

1. The __typeMatch__ expression is executed, and a list of Json tokens (JToken) are obtained.
1. Build a new Measurement for each JToken. Extract the above values (i.e. patientId, encounterId) from the JToken
    1. Extract a measurement property for each defined __valueExpression__ from the JToken

### Supported Expressions
To provide value to the end user and to enable the proposed feature, we should support at minimum the following expressions

- Mathmatical functions (add, substract, multiply, divide)
- String functions (length, insert, append)
- Array functions (coalesce, length, insert, append)
- Date functions (parseUnixTimestamp, dateFormat)

### Template Languages

#### Liquid
[Liquid](https://shopify.github.io/liquid/) is a markup language to define a document generation template running within its own framework. As a template engine its main purpose is to manipulate and produce a document of any type. Through various control flows, loops and functions any document can be manipulated and dynamically generated.

Additional functionality can be added via .Net classes.

**Example:** Calculate height from separate values
Given the input data
```json
{
  "preflang": "en_EN",
  "birthdate": "12345678",
  "gender": 1,
  "shortname": "ABC",
  "measures": [
    {
      "value": 180,
      "unit": -2,
      "timestamp": 1622655433,
      "type": 4
    },
    {
      "value": 8000,
      "timestamp": 1622655433,
      "unit": -2,
      "type": 1
    }
  ],
  ...
}
```

The following generates the string __1,80m__
```json
{
  "templateType": "ExpressionContentTemplate",
  "template": {
      "typeName": "rangeofmotion",
      "typeMatchExpression": "$.measures[?(@.type == 4)]",
      "patientIdExpression": "$.SystemProperties.iothub-connection-device-id",
      "values": [
          {
              "required": "true",
              "valueExpression": {
                  "expressionLanguage" : "Liquid",
                  "expression" : "
                  {%- assign height       = value | downcase -%}
                  {%- assign heightLength = height | size -%}
                  {%- assign insertPos    = heightLength | plus: unit -%}
                  {%- assign prefix       = height | slice: 0, insertPos  -%}
                  {%- assign suffix       = height | slice: insertPos, heightLength -%}
                  {{- prefix | append: ',' | append: suffix | append: 'm' -}}"
              },
              "valueName": "height"
          }
      ]
  }
},
```

**Example:** Convert Posix time into ISO DateTime
Given the input data
```json
{
  "posixTime" : 1622655433
}
```

The following generates the string __2021-06-04T07:48:27__
```json
{
  "templateType": "ExpressionContentTemplate",
  "template": {
      "typeName": "rangeofmotion",
      "typeMatchExpression": "$..[?(@Body.RangeOfMotion)]",
      "patientIdExpression": "$.SystemProperties.iothub-connection-device-id",
      "timeExpression": {
        "expressionLanguage" : "Liquid",
        "expression" : "{{-  posixTime | date: '%Y-%m-%dT%H:%M:%S' -}}"
      },
      "values": [
          ...
      ]
  }
},
```
##### Pros
- Larger library of built-in functions
- More active open source community

##### Concerns
A detailed overview of the capabilities and concerns with Liquid can be found [here](https://microsofthealth.visualstudio.com/Health/_git/health-poet-docs?path=%2Fwiki%2Fspecs%2FResearch%253A-Template-Authoring.md&version=GBmaster&_a=preview). 

In addition to these concerns, below are several more

- Liquid is a template language and can produce documents of any type. It can output multiple values through through the use of its display function __{{ }}__. For our purpose we require a single value to be output after the function is evaluated, with the added constraint that the value be a valid Json type. It is very easy for the customer to create a valid Liquid expression that does not meet these requirements.
- Need to provide expressions that can be stored inside of Json String. This may involve escaping characters which is error prone if done by a human. The escaped characters would need to be removed before processing the expression
- Validation of Liquid is a challenge. While we can exectue the expression and get a non-error, it's not ensured that we get a value that meets IotConnector requirements as specified in [Expression Evaluation](#Expression%20Evaluation)
- The .Net implementation is not 100% identical with the original Ruby implementation. This can lead to confusion with customers if they create expressions based on knowledge of the Ruby implementation.
- __Will need a Template Authoring Experience to be fully usable by customers.__
- Complex Json structues, such as arrays, need to be properly built by the customer. For example:

Right
```
{% assign values = "1, 2, 3, 4" | split: ", " %}
[ {{ values | join: "," }} ]
```
Wrong (produces a trailing comma)
```
[
  {% for v in values %}
    {
        "name": "{{v}}"
    },
  {% endfor %}
]
```

#### JMESPath
[JMESPath](https://JMESPath.org/) is a query language for JSON. Similar to JsonPath, it allows one to access and query for elements of a Json Document. It goes farther than this by allowing users to process extracted elements through one or more functions in order to manipulate the data. The output of a successful JMESPath expression will be a Json object.

**Example:** Calculate height from separate values
The basic JMESPath does not give us the functions needed to do this. Given a custom function of 'insert(string, valToInsert, pos)' and 'append(string, valToAppend)', this could be achieved as follows:
Given the input data
```json
{
  "preflang": "en_EN",
  "birthdate": "12345678",
  "gender": 1,
  "shortname": "ABC",
  "measures": [
    {
      "value": 180,
      "unit": -2,
      "timestamp": 1622655433,
      "type": 4
    },
    {
      "value": 8000,
      "unit": -2,
      "timestamp": 1622655433,
      "type": 1
    }
  ],
  ...
}
```

The following generates the string __1,80m__
```json
{
  "templateType": "ExpressionContentTemplate",
  "template": {
      "typeName": "rangeofmotion",
      "typeMatchExpression": "$.measures[?(@.type == 4)]",
      "patientIdExpression": "$.SystemProperties.iothub-connection-device-id",
      "values": [
          {
              "required": "true",
              "valueExpression": {
                  "expressionLanguage" : "JMESPath",
                  "expression" : "measures[?type == `4`] | @[0] | insert(to_string(value), ',', sum(length(to_string(value)), unit)) | append(@, 'm')"
              },
              "valueName": "height"
          }
      ]
  }
},
```

**Example:** Convert Posix time into ISO DateTime
The basic JMESPath does not give us the functions needed to do this. Given a custom function of 'parseUnixTime', this could be achieved as follows:
```json
{
  "posixTime" : 1622655433
}
```

The following generates the string __2021-06-04T07:48:27__
```json
{
  "templateType": "ExpressionContentTemplate",
  "template": {
      "typeName": "rangeofmotion",
      "typeMatchExpression": "$..[?(@Body.RangeOfMotion)]",
      "patientIdExpression": "$.SystemProperties.iothub-connection-device-id",
      "timeExpression": {
        "expressionLanguage" : "JMESPath",
        "expression" : "parseUnixTime(posixTime)"
      },
      "values": [
          ...
      ]
  }
},
```

##### Pros: JMESPath
- Performance is better than Liquid
- Works with and produces Json objects. DotNet implementation uses Newtonsoft Json Library, which is what we use today

##### Concerns: JMESPath
- Would need to build and maintain custom functions to achieve our use cases. These could be contributed back into the open source projects.
    - Allowing customers to test their templates with our custom functions will be a challenge. We'd need to allow this within the Template Authoring Experience
- Need to provide expressions that can be stored inside of Json String. This may involve escaping characters which is error prone if done by a human. The escaped characters would need to be removed before processing the expression
- __Will need a Template Authoring Experience to be fully usable by customers.__

Scope

As detailed in this open [Pull Request](https://github.com/jmespath/jmespath.site/pull/6/files/ae5ad3f590ae92c9789f3e5cacd99726ea028b74#diff-8f58dd34123874837acec4100110fb28bb1c8df3c4a9e81408e9c4a4775e86ae), it can be complicated to build a JMESPath function that requires data from a parent scope. Further explaination of this issue can be found on this [StackOverflow](https://stackoverflow.com/questions/57223274/how-to-insert-a-attribute-with-a-dynamic-value-in-many-object-or-add-an-element).

From the doc:
```
As a JMESPath expression is being evaluated, the current element, which can be
explicitly referred to via the ``@`` token, changes as expressions are
evaluated.  Given a simple sub expression such as ``foo.bar``, first the
``foo`` expression is evaluted with the starting input JSON document, and the
result of that expression is then used as the current element when the ``bar``
element is evaluted.  Conceptually we're taking some object, and narrowing down
its current element as the expression is evaluted.

Once we've drilled down to a specific current element, there is no way, in the
context of the currently evaluated expression, to refer to any elements outside
of that element.  One scenario where this is problematic is being able to refer
to a parent element.
```

For example, given the input data
```json
{
  "unit": -2,
  "measures": [
    {
      "value": 180,
      "type": 4
    },
    {
      "value": 8000,
      "type": 1
    }
  ],
  ...
}
```

The following expression would not be able to access the 'unit' value, as it exists inside of the parent scope
```json
{
"expression" : "measures[?type == `4`] | @[0] | insert(to_string(value), ',', sum(length(to_string(value)), unit)) | append(@, 'm')"
}
```

While we currently don't have a use case which this would be an issue it's easy to see how eventually this may be reported by the customer. If we choose to address this (potentially via by providing a reference to the root such as '$') we'd need to maintain a custom port of the Dotnet codebase. A reference to the root parent is currently not a part of the JMESpath spec.

Another potential solution would be to allow users to specify multiple expressions, where the result of each expression would be assigned to a variable and could be used in subsequent expressions:

```json
{
"expression" : "
  var myMeasure = measures[?type == `4`] | @[0]
  var myString = insert(to_string(myMeasure.value), ',', sum(length(to_string(myMeasure.value)), unit))
  myString.append(@, 'm')
"
}
```

In the above, each expression for the variable assignment is evaluated. The result is added as a new JToken to the root of the original Json object. This updated Json object is then used in the following expressions. In this way, the following statements can access both preceeding variables and the original parent scope inside of their expressions.

The final expression is treated as the overall return value. In the above example, the final expression __myString.append(@, 'm')__ would work on a Json object like the one below:

```json
{
  "unit": -2,
  "measures": [
    {
      "value": 180,
      "type": 4
    },
    {
      "value": 8000,
      "type": 1
    }
  ],
  "myMeasure" : {
      "value": 180,
      "type": 4
  },
  "myString" : "1,80"
}
```

## Benchmark Tests
Benchmark tests were run against __Liquid__ and __JMESPath__ in order to evaluate performance in a variety of scenarios. The framework used was [BenchmarkDotNet](https://benchmarkdotnet.org/articles/guides/how-it-works.html). The tests focused on the time it took each framework to evaluate an expression, and to guage how they perform related to each other. As such the following pre-requisit steps were take:
- Eventdata was pre-loaded before tests began and compiled into appropriate objects (JToken for JMESPath and Dictionary for Liquid). This would simulate incoming EventData being compiled once and used multiple times by Templates.
- Expressions were pre-compiled before tests began. 
- A simple JsonPath expression ("$.shortname") to select a value was used as the baseline
- For each expression language, the following expressions were performed:
    - Calculated Function to convert a measure into a height value of __1,80m__
    - Function to convert unix timestamp into a DateTime string
    - Select a value from a object
- Expressions ran in the same process as the test (i.e. no remote code execution)

The following json was used to simulate eventData:

```json
{
  "preflang": "en_EN",
  "birthdate": "12345678",
  "gender": 1,
  "shortname": "ABC",
  "timestamp": 1622655433,
  "measures": [
    {
      "value": 180,
      "unit": -2,
      "type": 4
    },
    {
      "value": 8000,
      "unit": -2,
      "type": 1
    }
  ]
}
```

The results of the testing can be seen below:

``` ini

BenchmarkDotNet=v0.13.0, OS=macOS Catalina 10.15.7 (19H1030) [Darwin 19.6.0]
Intel Core i9-9880H CPU 2.30GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=5.0.301
  [Host]     : .NET Core 3.1.16 (CoreCLR 4.700.21.26205, CoreFX 4.700.21.26205), X64 RyuJIT
  DefaultJob : .NET Core 3.1.16 (CoreCLR 4.700.21.26205, CoreFX 4.700.21.26205), X64 RyuJIT


```
|                                     Method |          Mean |        Error |       StdDev |           Min |           Max |        Median |  Ratio | RatioSD |
|------------------------------------------- |--------------:|-------------:|-------------:|--------------:|--------------:|--------------:|-------:|--------:|
| Baseline_Parse_And_Run_JsonPath_GetElement |     222.67 ns |     1.982 ns |     1.757 ns |     219.83 ns |     226.02 ns |     222.45 ns |   1.00 |    0.00 |
|                Liquid_Benchmark_GetElement |   2,855.42 ns |    21.115 ns |    19.751 ns |   2,821.88 ns |   2,883.42 ns |   2,857.89 ns |  12.82 |    0.15 |
|              JmesPath_Benchmark_GetElement |      70.70 ns |     0.755 ns |     0.707 ns |      69.53 ns |      72.10 ns |      70.71 ns |   0.32 |    0.00 |
|           Liquid_Render_CalculatedFunction | 212,325.66 ns | 1,416.459 ns | 1,255.654 ns | 210,770.34 ns | 214,887.20 ns | 212,282.72 ns | 953.62 |    9.91 |
|         JmesPath_Render_CalculatedFunction |   6,180.73 ns |   102.046 ns |   132.689 ns |   6,042.35 ns |   6,593.27 ns |   6,118.09 ns |  27.99 |    0.69 |
|                    Liquid_Render_ParseUnix | 120,872.95 ns | 2,407.790 ns | 4,752.743 ns | 112,387.38 ns | 130,623.62 ns | 120,176.15 ns | 521.28 |   12.18 |
|                  JmesPath_Render_ParseUnix |     328.15 ns |     1.458 ns |     1.364 ns |     325.21 ns |     331.11 ns |     328.28 ns |   1.47 |    0.01 |

## Approach 
Based on the above information, the following approaches can be taken to implement Calculated Functions

#### Do not use enhanced expression language
In this approach, we do not include an advanced expression language at all. We simply implement the Normalized Data -> Customer Event Hub. The customer can then perform the calculation on their end and route the updated data back into the IotConnector.

Pros:
- Only need to implement Normalized Data -> Customer Event Hub

Cons:
- Extra development work for the customer.
- May not be available for Public Preview

#### Utilize JMESPath (In Process)
Given the performance differences between JMESPath and Liquid, in this approach we implement a new template type that supports JMESPath. This template invokes operations directly inside of the normalization process. We also create the base set of functions needed to support the current use cases as well as expected future use cases.

Pros:
- Provides a declaritive way for end users to add functions to their normalization pipeline
- Change only affects Normalization code, which is a smaller amount of work
- Better chance of making Public Preview 

Cons:
- Tied to DotNet version of JMESPath, which may not have as much support as other language implementation

#### Utilize JMESPath (RPC)
Given the performance differences between JMESPath and Liquid, in this approach we implement a new template type that supports JMESPath. This template invokes operations via RPC. We also create the base set of functions needed to support the current use cases as well as expected future use cases.

Pros:
- Provides a declaritive way for end users to add functions to their normalization pipeline
- Can run any port of JMESPath
- Can potentially support multiple versions of JMESPath at once. Different versions can be running inside of different Pods.
- Extra controls can be enforced on Pod processing expressions (permissions, network ingress/egress) 

Cons:
- More development time is needed as this will touch multiple components (Normalization, .Net K8s Api, Go Controller, Devops build system)

## Alternative Ideas

- Build an ADF Pipeline which performs the normalization. Can potentially be exposed to customers so that they can 'extend' the pipeline with custom processing steps

# Support Multiple Scopes
As defined in section [Expression Evaluation](#Expression%20Evaluation), each expression is evaluated in the scope of the object returned from the __typeMatchExpression__. Data outside of this scope cannot be accessed. For example, given the below example, a match expression of __measures[?type == `4`]__ would produce an object that could not reference the value __unit__.

```json
{
  "unit": -2,
  "measures": [
    {
      "value": 180,
      "type": 4
    },
    {
      "value": 8000,
      "type": 1
    },
    {
      "value": 110,
      "type": 4
    },
  ],
  ...
}
```

To address this, we can add each object produced by the __typeMatchExpression__ to a new Json Object that contains the elements of the original root object plus the extracted value. The object as well as the original data would be at the same scope, and both could be used in subsequent expressions.

For example, given the match expression __measures[?type == `4`]__, two new Json objects would be created and processed by the normalization pipeline:

```json
{
  "unit": -2,
  "measures": [
    {
      "value": 180,
      "type": 4
    },
    {
      "value": 8000,
      "type": 1
    },
    {
      "value": 110,
      "type": 4
    },
  ],
  "extractedValue" : {
      "value": 180,
      "type": 4
    },
}
```

```json
{
  "unit": -2,
  "measures": [
    {
      "value": 180,
      "type": 4
    },
    {
      "value": 8000,
      "type": 1
    },
    {
      "value": 110,
      "type": 4
    },
  ],
  "extractedValue" : {
      "value": 110,
      "type": 4
    },
}
```

Subsequent expressions may now interect with the additional values:
```json
{
"expression" : "multiply(extractedValue.value, unit)"
}
```

