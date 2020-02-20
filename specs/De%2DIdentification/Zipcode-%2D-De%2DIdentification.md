## Investigation doc on Zipcode De-Identification

**Default De-Identification all digits on zipcode**

This is current implementation and will be the default solution. If nothing is specified in the parameter section of a configuration file, the default behavior of the De-Id engine is to redact all digits of zipcodes. 



**More specific De-Identification by leaving first 3 digits of Zipcodes except those with polulation less 20K**
 
Our design provides user an option to keep first 3 digits of zipcode by following HIPAA guidance of safe harbour method on zipcode. 

Covered entities may include the first three digits of the ZIP code if, according the current publicly availabe data from the Bureau of the Census: (1) The geographic unit formed by combining all ZIP codes with the same three initial digits contains more than 20,000 people; or (2) the initial three digits of a ZIP code for such geographic units containing 20,000 or fewer people is changed to 000. 

Configuraiton method, instead of "redact", "partialZCTA" is used to enable this option, with Paramenter configuration to specify those 20,000 or fewer ZIP codes. 

Example Configuration for the specific approach is like the following (based on 2010 Census Data)

```sh


"parameters": {
    "dateShiftKey": "",
    "enablePartialAgesForRedact": true,
    "enablePartialDatesForRedact": true,
    "enablePartialZipCodesForRedact": true,
    "restrictedZipCodeTabulationAreas": [
        "036",
        "059",
        "102",
        "203",
        "205",
        "369",
        "556",
        "692",
        "821",
        "823",
        "878",
        "879",
        "884",
        "893"
    ]
}