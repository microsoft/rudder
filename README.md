# rudder
Static analysis of MSIL based on the analysis-net infrastructure

## Installation
Rudder has a source relationship with the infrastructure it is built on top of.
The three projects, Backend, CCIProvider, and Model, are not in this repo.
They are part of a project [analysis-net](https://github.com/edgardozoppi/analysis-net).
Currently they build against a [fork](https://github.com/garbervetsky/analysis-net) branch cci-version that you can get from [here](https://github.com/garbervetsky/analysis-net/tree/cci-version).

The solution file assumes that you have mapped that repo into a sub-folder of the Rudder root folder named "analysis-net".

We know this is not optimal and hope to move to a binary (NuGet) dependence soon.
