# PR Pipeline Optimization

In the context of the `health-paas` repo, this design proposes shifting the onus of validation left to the developer and the onus of verification right to post-checkin automation. These shifts empart additional trust on `health-paas` developers to validate their changes in a way they deem appropriate and begins to optimize pipelines on the assumption that most checkins are valid and safe. In return for empowering `health-paas` developers with validating their own changes before a pull request, we should be able to significantly decrease the time-to-completion for pull requests and begin a journey of reducing a change's overall time-to-production.

[[_TOC_]]

# Business Justification

According to the [HLS Cloud & Data June 2021 Developer Velocity Survey Report](https://microsofthealth.visualstudio.com/Health/_wiki/wikis/Health.wiki/471/June-2021-Developer-Velocity-Survey-Report), the time and reliability of the health-paas repo's [Resolute_PaaS_PR_Parallel](https://microsofthealth.visualstudio.com/Health/_build?definitionId=366&_a=summary) pipeline was listed as a significant area of concern for developer productivity and well-being. 

In the book _[Accelerate: Building and Scaling High Performing Technology Organizations](https://microsoft.sharepoint.com/sites/library/SitePages/Bib/Details.aspx?bibid=468027&OR=Teams-HL&CT=1623949984543), by [Dr. Nicole Forsgren](https://www.linkedin.com/in/nicolefv/), et. al._, the authors concluded from four years of research on capabilities and practices required for accelerating software development and delivery that increasing the agility of software changes increases both the stability and speed of deployments:

    "Shorter product delivery lead times are better since they enable faster 
    feedback on what we are building and allow us to course correct more rapidly. 
    Short lead times are also important when there is a defect or outage and we 
    need to deliver a fix rapidly and with high confidence."
    - Accelerate, Chapter 2: Measuring Performance

# Change Description

This design proposes reducing the [Resolute_PaaS_PR_Parallel](https://microsofthealth.visualstudio.com/Health/_build?definitionId=366&_a=summary) automation from the current six sections, averaging 2.5 hours total runtime:
 1. Build (includes build, unit tests, code signing, and packaging)
 2. Deploy Environment
 3. Deploy Applications
 4. Provision Test Accounts & Function Tests
 5. Rotate Admin Password
 6. SDL 

...to two sections, averaging 20 minutes total runtime)
   
 1. Build (includes build and unit tests)
 2. SDL

The sections removed from the validation pipeline will be moved to a new or existing CI pipeline that will run either in a rolling capacity or regular schedule. Failures in this CI pipeline will result in either immediately rolling-back the offending change from the `health-paas/master` branch or fixing forward **only if the fix is straight-forward and has high-confidence**. 

The secondary DRI currently has responsbility of monitoring `health-paas` pipelines and is empowered to investigate, delegate, and rollback changes when required.

# Scenarios

1. A `health-paas` developer prepares to send a pull request to the default branch (i.e. `master`)
2. A `health-paas` developer sends an initial pull request to the default branch.
3. A `health-paas` developer updates an existing pull request to the default branch.
4. A `health-paas` pull request into the default branch is completed.
5. `health-paas` releases to production.

# Metrics

- CI verification failure counts (not due to test instability).
- Time to track CI failures to the breaking change.
- Pull request time-to-completion to `health-paas/master`.
- Developer satisfaction

# Design
1. A `health-paas` developer prepares to send a pull request to the default branch (i.e. `master`)

   Before sending a pull request for review, a `health-paas` developer is expected to run validations they believe are appropriate for their changes. This may include some, and occassionally all, of the below validation items as well as other validations specific to the changes being made. 
   - Unit tests pass.
   - Test deployment for existing cloud deployed environment.
   - Test deployment for new cloud deployed environment.
   - Test deployment for locally deployed environment. (1)
   - Integration tests pass.
   - Validate bug/feature scenarios in a locally deployed environment. (1)
   - Validate bug/feature scenarios in a cloud deployed environment.
   - Test localbuildanddeploy script
   - Validate ARM changes.
   - Update environment and environmentGroup templates.

_(1) Local environments for Jupiter/Gen2 work are not currently supported due to dependencies on SQL._

2. A `health-paas` developer sends an initial pull request to the default branch.
   
   On submition of a pull request, the updated [Resolute_PaaS_PR_Parallel](https://microsofthealth.visualstudio.com/Health/_build?definitionId=366&_a=summary) runs (build and unit tests only) as gate to check-in. All other existing check-in gates remain in-place.
  
3. A `health-paas` developer updates an existing pull request to the default branch.
   
   On an update of an existing pull request, any existing gate automation is cancelled and the updated [Resolute_PaaS_PR_Parallel](https://microsofthealth.visualstudio.com/Health/_build?definitionId=366&_a=summary) runs (build and unit tests only) as gate to check-in. All other existing check-in gates remain in-place.

4. A `health-paas` pull request into the default branch is completed.
   
   On completion of the pull request, the change is included in the next regular CI pipeline.

5. `health-paas` release to production
   
   Releases should only be pulled from commits that have been successfully verified by the regular CI pipeline.

# Test Strategy

In this initial change proposal, it is prudent to note there are no changes to overall test coverage. 

# Security

There are no additional security concerns aside from what is already implemented. Additional security improvements are out-of-scope for this design.

