using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Validation;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Model;

namespace FHIRValidator
{
    class Program
    {
        public static IConfiguration Configuration { get; set; }
        static void Main(string[] args)
        {

            var builder = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddCommandLine(args);

            Configuration = builder.Build();

            var fileName = Configuration["FileName"];
            var profile = Configuration["Profile"];

            string resourceText = "";
            try
            {  
                using (StreamReader sr = new StreamReader(Configuration["FileName"]))
                {
                    resourceText = sr.ReadToEnd();
                }
            }
            catch (IOException e)
            {
                Console.WriteLine("The file could not be read:");
                Console.WriteLine(e.Message);
            }

            var parser = new FhirJsonParser();
            var source = new CachedResolver(new MultiResolver(
                    new DirectorySource(@"./profiles"),
                    ZipSource.CreateValidationSource())
                );

            // prepare the settings for the validator
            var ctx = new ValidationSettings()
                    {
                        ResourceResolver = source,
                        GenerateSnapshot = true,
                        Trace = false,
                        EnableXsdValidation = true,
                        ResolveExteralReferences = false
                    };

            var validator = new Validator(ctx);


            try
            {
                var parsedResource = parser.Parse(resourceText);
                Console.WriteLine(parsedResource.TypeName);

                OperationOutcome result;
                if (string.IsNullOrEmpty(profile)) 
                {
                    result = validator.Validate(parsedResource);
                }
                else
                {
                    result = validator.Validate(parsedResource, profile);
                }

                Console.WriteLine(result.ToString());
            }
            catch (FormatException fe)
            {
                Console.WriteLine("Resource could not be parsed");
                Console.WriteLine(fe);
            }
        }
    }
}
