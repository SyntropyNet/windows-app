[![Release Application](https://github.com/SyntropyNet/windows-app/actions/workflows/pipelines.yml/badge.svg)](https://github.com/SyntropyNet/windows-app/actions/workflows/pipelines.yml)

# Syntropy Windows Agent App

#### [Latest SYNTROPY Agent Docs](https://docs.syntropystack.com/docs/start-syntropy-agent) 
- https://docs.syntropystack.com/docs/start-syntropy-agent

#### Prerequisites

* Wireguard installed

* .Net Framework 4.7 Installed

#### Limitations

* Docker network subnets can't overlap.
* 10.69.0.0/16 is used for internal Wireguard network

#### Steps
1. Login to [https://platform.syntropystack.com](https://platform.syntropystack.com) 
2. Create API key (Settings > API keys)
3. Install SYNTROPY Agent
    - Launch the appropriate MSI installer (x32 or x64) and follow the instructions of the installation wizard. The installer will automatically check the .Net Framework 4.7 requirement.

#### Releases
* Current releases starting with v1.0.7 can be found in this repository.
* Older releases can be [found here](https://github.com/SyntropyNet/windows-application)
