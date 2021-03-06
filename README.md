<h4 align="center">
  <img alt="common readme" src="https://i.imgur.com/5zpfhLm.png" width="306.3" height="350">
</h4>
<h4 align="center">
  The really friendly discord bot that helps you make channel pairs
</h4>
<h4 align="center">
  <a href="https://discord.gg/GPCkUX"><img src="https://badgen.net/badge/Gabby's%20Birdhouse/Join/7289DA?icon=discord"</a>
  <a href="https://app.gitkraken.com/glo/board/Xtt2HbruPAARFSN9"><img src="https://badgen.net/badge/Glo%20Board/View/149287?icon=https://svgshare.com/i/Fr5.svg"</a>
</h4>


[Gabby Picture by DashieSparkle](https://www.deviantart.com/dashiesparkle)

---

## What does she do?
Gabby lets Discord server owners and managers create channel pairs. She also tracks who is connected to those channels and will show/hide the text channel in the pair when they connect or disconnect repspectively.

## What is a channel pair?
There might be a better name for it, but a channel pair is a text and a voice channel in discord that are directly linked.

In TeamSpeak each voice channel has a text chat attached to it. Only those connected to the channel could see that chat which made things nice and clean. Discord as of yet hasn't added the same behaviour so a lot of Discord servers emulate this feature by making a pair of channels that sit next to each other in the tree like below.

![Picture of Channel Pair](https://i.imgur.com/OLV4CcF.png)

I feel like this is messy (especially with more than one) and doesnt give the same effect as people outside of the voice channel can continue to type in the text chat.

## What can Gabby do to help?
Gabby helps by setting up these channel pairs and monitoring them so that when a user joins the voice channel of a pair, then the text channel will appear for them and dissapear when they disconnect.

![Demonstration of Gabby](https://i.imgur.com/585lsai.gif)

She does this by using a role that is named like the pair. The channel permissions on the text channel are set so that everyone cannot see it, but the pair role can. Gabby then assigns and removes this role from users as they connect and disconnect respectively.

Gabby also features some commands that lets server owners set up these channel pairs with ease and remove them just as quickly.

## How can I use her on my server?
Currently I'm still perfecting functionality and making her more reliable. Once this is done I may consider hosting her publicly.

If you wish to use her now then feel free to clone this repo and use `dotnet run` to build and run the project.
Gabby is built on .NET Core 3.1 using the Discord.Net API. 

Currently I am using AWS DynamoDB to allow her to store information on channel pairs, the settings structure for which can be found in the `_configSample.yml` file. 

DynamoDB is in the Free Tier for AWS up to a total of 25GB of data storage so setting one up should be simple. Then just provide the IAM access and secret token with permission to read/write to DynamoDB along with the region code your using into the `_config.yml` and your good to go.

**NOTE: Make sure to put your Discord bot token along with the above ina new file called `_config.yml` file, following the structure of `_configSample.yml`**
