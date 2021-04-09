# Syntropy Windows Agent App

---

#### [Latest SYNTROPY Agent Docs](https://docs.syntropystack.com/docs/start-syntropy-agent) 
- https://docs.syntropystack.com/docs/start-syntropy-agent
#### Prerequisites

* Wireguard installed

* .Net Framework 4.7 Installed
```sh
docker system info
```
---
#### Limitations

* Docker network subnets can't overlap.
* 10.69.0.0/16 is used for internal Wireguard network

#### Steps
----
##### 1. Login to [https://platform.syntropystack.com](https://platform.syntropystack.com) 
---
##### 2. Create API key (Settings > API keys)

---

##### 3. Install SYNTROPY Agent

Details:

###### From MSI installer

Launch the appropriate MSI installer (x32 or x64) and follow the instructions of the installation wizard.
The installer will automatically check the .Net Framework 4.7 requirement.

More information: 

---