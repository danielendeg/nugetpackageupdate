# Overview
Azure Function App support customer deploy serverless application with pay-per-use pricing model. With Azure Functions, the cloud infrastructure provides all the up-to-date servers to keep application running at auto scale. Function App also supports long running application with durable mode which is supported by ADF. 

Customer can use De-Identification function app integrate with ADF pipeline or use it as a standalone service integrate with other service.

# User scenario with Function App 
1. Integrate with ADF Pipeline
![ADF Flow.jpg](/.attachments/ADF%20Flow-7ebd3c80-ce3a-4af8-b76c-baaee97e494a.jpg)
- In this ADF pipeline, we trigger durable Function for every storage blob in the container, the function app would upload the result to destination container.
- We can suggest customer do following operations (copy activity, data flow activity...) after all blob upload complete or in the foreach loop. 

2. Stand-alone Function App as web service