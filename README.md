# Resonant Orbit Calculator v0.1.2
Calculate resonant orbits for the selected vessel and assit with setting up maneuver nodes to enter the resonant orbit
![Resonant Orbital Calculator](https://i.imgur.com/uojtZfN.png)
Making resonant orbit planning easier for Kerbal Space Program 2 one mission at a time.

## Compatibility
* Tested with Kerbal Space Program 2 v0.1.1.0.21572
* Requires SpaceWarp 0.4+
## Features
* Display Carrier Vessel and Current Orbit Info
* Assist In planning Deployment Missions for constellations of 2 or more satellites deployed
* Model Resonant Deployment Orbit
* Display effects of the next planned Maneuver
## Planned Improvements
To see what improvements and new features are planned for this mod, you can visit the Issues page on the project's GitHub.
## Installation
1. Download and extract SpaceWarp into your game folder.
1. Download and extract this mod into the game folder. If done correctly, you should have the following folder structure: <KSP Folder>/BepInEx/plugins/resonant_orbit_calculator.
## Sales Pitch
Would you like to know just what orbit to get your craft into so it's ready to deploy a constellation of satellites? Are you in a hurry and don't want to spend time performing tedious computations involving obscure and arcane orbital mechanics equations? Well, now you can! Look no further - Resonant Orbit Calculator is at hand and ready to help you sort out your next Comm Sat deployment mission in a snap!
![Resonant Orbital Calculator UI](https://i.imgur.com/hY8y7kW.png)
## Marketing Materials
With **Resonant Orbit Calculator** (ROC) by your side, you'll quickly be able to see all the important details for the **Current Orbit** your **Carrier Vessel** is in, as well as being able to plan your **Deployment Orbit**.

No matter if your deploying 2 **Payloads** or 52, the ROC will get you all set up with everything you need to know. Just click the **(-)** and **(+)** buttons next to the number of satellites you're carrying and you'll be off to the races!

Would you like to only deploy a satellite once every 2nd or 3rd or 27th pass? We've got you covered there, too! Just click away with the **(-)** and **(+)** buttons to quickly set your **Deploy Orbits** from 1 each pass on up to whatever you need! The ROC will even tell you exactly what **Orbital Resonance** factor will be needed to execute your plan!

Have you got a specific **Target Altitude** you need your satellites in? There's a handy spot right there ready for you enter any value you like, and you'll instantly be able to see the orbital period your deployed sats will have (assuming circular orbits).

What if you want to set your satellites up so they're in **Synchronous** or **Semi-Synchronous** orbits? That info is displayed for you (assuming such orbits are possible for the body you're in orbit about). You don't even need to transcribe this back into the Target Altitude field! Just click on the handy *Set Target Altitude* button **⦾** right next to the displayed Synchronous or Semi Synchronous Alt field. Note: These buttons are only present if such orbits are possible...

If you're seeing the dreaded *Outside SOI* warning for either of these, then don't fret! The **SOI Alt** is also displayed so you'll now what the max is!

But what if you don't care about the max and are more interested in the **Minimum LOS Orbit Alt**? We've got you covered there, too! *(For constellation with 3 or more satellites only...)*. You'll find a handy *Set Target Altitude* button **⦾** here too! You can even account for **Occlusion** due to a planet's atmosphere (if it's got one). Toggle Occlusion on will allow you to set the **Atm** and **Vac** factors to apply, and will automatically apply the appropriate one for the body you're orbiting to set the Min LOS Orbit altitude accordingly.

What if you'd just like to target your **Current Orbit's Apoapsis** or **Periapsis** for deployment operations? There are handy *Set Target Altitude* buttons **⦾** right beside your **Apoapsis** (used for diving resonant orbits) and **Periapsis** (for climbing resonant orbits).

When it comes time to set up your **Deployment Orbit**, you've got an option to either **Dive** under your target orbit or climb higher for your resonant deployment orbit. Either way works in general, but watch out for *lithobraking Periapsis* when diving. Similarly watch out for *SOI escaping Apoapsis* when climbing.

Your choice for Diving or Climbing doesn't just impact opportunities for lithobraking and SOI escape, you'll also be able to quickly see the required **Injection ∆v** your satellites will need to supply in order to circularize their orbits when you release them! This is not to be confused with whatever ∆v you may need to get your Carrier Vessel into the deployment orbit, but this does need to be accounted for so that you'll know your satellites are prepared to complete their own circularization when you deploy them!

What about getting your craft into the Deployment Orbit? Just add a maneuver node to your current orbit and you'll be able to see where it's taking you in the **Maneuver** section! You can quickly get set up with a new Apoapsis or Periapsis that works for your plan to either perform a diving or climbing resonance respectively. Once that's set, you'll be able to use the Maneuver section once again to help you plan the next maneuver node to complete things and put you in the resonant orbit of your dreams!

![Resonant Orbital Calculator with Maneuver Node](https://i.imgur.com/zdYNNVt.png)
## Links
* SpaceDock: https://spacedock.info/mod/3332/Resonant%20Orbit%20Calculator
* Forum: https://forum.kerbalspaceprogram.com/index.php?/topic/215650-resonant-orbit-calculator/
* Example Rocket (shown above): https://forum.kerbalspaceprogram.com/index.php?/topic/213170-commsat-deployer/

The CommSat Deployer forum post includes detailed notes about how to stack satelites inside a fairing using a combination of engine mounts, Clamp-o-Tron Jr's and tiny decouplers.