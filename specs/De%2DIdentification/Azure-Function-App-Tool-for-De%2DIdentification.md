# Overview
Azure Function App support customer deploy serverless application with pay-per-use pricing model. With Azure Functions, the cloud infrastructure provides all the up-to-date servers to keep application running at auto scale. Function App also supports long running application with durable mode which is supported by ADF. Customer can use the function app as a standalone tool not only for web request. 

# Integrate with ADF Pipeline
In this ADF pipeline, we trigger durable function for each storage blob in the container, the function app would upload the result to destination container. Customer can extend the pipeline with following activities like copy, data flow... 

![ADF Flow.jpg](/.attachments/ADF%20Flow-7ebd3c80-ce3a-4af8-b76c-baaee97e494a.jpg)

- 

# Security

# User scenario
1. Deploy function app
2. Deploy Data Factory with arm template
3. Trigger Pipeline Run

# Further improvement
1. Big Single File