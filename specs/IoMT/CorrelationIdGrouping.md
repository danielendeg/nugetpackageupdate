Add the ability in the IoMT connector to support non-time based grouping.

[[_TOC_]]

# Business Justification

The IoMT connector supports time based grouping (single instance, hour, and daily).  The existing groupings work well for infrequent measurements (single) or continuously streaming values (hour or daily) but fall short when the user of the system wants to create an observation of measurements around an activity like an exercise or physical therapy session.

In order to support creating observations and associating data around an activity the IoMT connector should support additional grouping criteria, a correlation id. Any measurements sharing a correlation id, type, and device id will land in the same observation.

# Scenarios

Scenarios are outlined in [User Story 74184](https://microsofthealth.visualstudio.com/Health/_workitems/edit/74184).

# Metrics

Existing metrics and telemetry will be sufficient.  These capture normalized measurements from inbound messages and measurements converted to FHIR observations.  A new error type will defined for a missing correlation id when the expression is defined but no match is found.  This will be recorded with existing error telemetry.

# Design

Several parts of the code need to be updated to support a new grouping parameter, correlation id.

## Normalization Logic
Add a new property, ```string CorrelationIdExpression { get ; }```, to JsonPathContentTemplate and IotJsonPathContentTemplate.  This will be used to extract the correlation id from the messages and supply the value to model.  If the expression is supplied but no value is found and exception will be thrown.  The expression will be ignored if it is null or empty in the template.

## Model
Add a new property, ```string CorrelationId { get; }```, to IMeasurement, IMeasurementGroup, and implementing classes.  This will be used to store the extracted correlation id value.

## Stream Analytics
Sample job in the project and ARM templates will be updated to include correlation id in the both the select statement and grouping criteria.

## MeasurementObservationGroup & MeasurementObservationGroupFactory
The existing ObservationGroup and ObservationGroupFactory will be refactored into renamed classes, ```TimePeriodMeasurementObservationGroup``` and ```TimePeriodMeasurementObservationGroupFactory``` respectively.  Two new implementations, ```CorrelationMeasurementObservationGroup``` and ```CorrelationMeasurementObservationGroupFactory``` will be added as additional implementations to support the correlation id based grouping.

The existing ```MeasurementObservationGroupFactory``` will be changed to choose the correct underlying implementation of ```IObservationGroupFactory``` based on the value of the ```PeriodInterval``` in the FHIR template.  Add new option will be added to the ```ObservationPeriodInterval``` enumeration, ```CorrelationId = -1```.  If the period interval in the template is set to ```CorrelationId``` MeasurementObservationGroupFactory choose the new ```CorrelationMeasurementObservationGroupFactory```.  Otherwise the ```TimePeriodMeasurementObservationGroupFactory``` will be used preserving existing functionality.  If ```CorrelationMeasurementObservationGroupFactory``` is configured to be used and a correlation id is not present in the measurement group and exception will be thrown and recorded as telemetry.

In addition IObservationGroup interface will be refactored to include a new method, ```string GetIdSegment();```.  This method will be used to generate part of the unique id for the observation.  Currently the FhirImportService has the responsibility for generating the id for the observation which includes the patient id, device id, type name, and the start and end date of the observation.  It is the last part, start and end date, that will be moved into the GetIdSegment logic of the IObservationGroup.  The start and end date are appropriate for the time based grouping but are no longer valid when grouping by correlation id.  Instead, the IObservationGroup for correlation id will return the id as the last part of the segment.

# Test Strategy

Update unit tests to cover changed and added classes.

Perform manual E2E tests with existing templates and add new ones to cover the correlation id scenarios.
