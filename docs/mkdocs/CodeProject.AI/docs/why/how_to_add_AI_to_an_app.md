---
title: How to add Artificial Intelligence to an existing Application
tags:
  - CodeProject.AI
  - Artificial-Intelligence
---
# How to add Artificial Intelligence to an existing Application

If you haven't already been asked to add Artificial Intelligence (AI) capabilities to an app then
it's probably only a matter of time before the topic is raised.

Adding AI capabilities isn't hard. However, that's like saying adding database support to an app 
isn't hard. It's not, but choosing the correct database, setting up the schema and stored 
procedures can be hard work. Then you need to decide whether the database should be on the same 
server, different server. You also need to decide which database you'll use: relational, document 
based, Key/Value... It can get complicated.

AI is just like that. Add a library, use a local service, use a hosted service, which service
do I use? How do I set it up. And then the tricky questions: how much will it cost? How will
my data be handled? How secure is it?

So let's do a quick walk through of your options so you at least know the questions to ask.

## Writing it yourself

I'll start by saying this is how we started our foray into AI a decade ago and I really 
wouldn't recommend it. There are so, so many brilliant researchers who have spent a zillion
man-hours building incredibly powerful and efficient AI libraries and models based on a
fast evolving corpus of research into AI that it's simply easier, faster and safer to use
one of the many AI solutions already available.

Having said that, diving into something like a simple neural network to build up the ability
to classify data based on your specific scenarios can be fulfilling, provide great results,
and will result in little overhead. CodeProject's SPAM filter is just such a beast and anything
bigger would, in our view, be overkill. The right tool for the job in this case.

**Pros**: 
 - It's fun writing code.
 - You get exactly what you need and nothing more
 - You may end up with a far smaller codebase since you're not importing libraries and all their
   dependencies

**Cons**
- You're reinventing the wheel
- You'll (probably) not do it as well, as accurately, or have a solution as fast as what is
  already out there.
- It may be a distraction from your core business case. It could end up costing you more in
  time, missed opportunities and developer time than simply using an existing solution.

## Using an AI library or toolkit directly in your code

If you wish to include AI processing directly in your code base then you can't go wrong using
libraries such as such as Tensorflow or PyTorch. There are lots of mature, supported, easy to use
libraries available for multiple languages and platforms. They take care of the hardwork for you,
and together with the many pre-trained models out there, all you need to do is include the toolkit,
load the model, input your data, run the inference and output the results.

Here's how to use the latest YOLO5 model in python:

```python
import torch                                            # import the Torch library

model = torch.hub.load('ultralytics/yolov5', 'yolov5s') # Load the YOLO model
img = '~/username/images/street.jpg'                    # Specify the image
results = model(img)                                    # Perform the inference
results.print()                                         # Output the results
```

How easy is that!

Issues start to arise when you need to cater for different model versions, and the versions 
of the libraries that the models were trained for. And the versions of the compiler or 
interpreter needed for the libraries that are needed for the models. And then what happens if 
all of this conflicts with the libraries, interpreter versions and even hardware requirements 
in other parts of your app?

It can be a real challenge, especially for, say, Python, where you may need to setup multiple
virtual environments, each with their own copies of the library packages, and each using a
different version of Python. Keeping these in sync and uncorrupted can take a lot of patience.

You may have a wonderful solution for, say, Python 3.7, but when it's run on another machine
that has Python 3.11 installed, it may simply fail.

Adding AI directly into your application can mean you will need to be extremely careful to ensure
you always deploy all the parts needed as one unit. Docker will save you here, but to many that 
kind of overhead may not be acceptable.

Finally, in adding an AI toolkit to your app you need to remember that you'll also be adding the 
model itself. AI models can be big, Gigabyte big.

**Pros**

- You build on the work of brilliant developers and researchers before you
- Many of the libraries are Open Source so you can view and audit the code
- AI libraries and tools are being developed and refined at breakneck speed. There is a constant 
  stream of new features and improved performance being released
- The libraries are generally very easy to use
- There are tools to allow conversion of models between libraries. 
- There's a model for almost any language and platform

**Cons**

- There's definitely a learning curve to using a library
- You may be restricted to using a particular model format for the given library
- There's so, so many libraries. The paradox of choice.
- Including a library rarely means just one library: it usually brings along all its friends and
  relatives and distant cousins. Things can get bloated
- Inluding a library means including the models. Things can get really, really big, fast.
- You have to ensure you keep your compiler/interpreter, libraries and models in sync with regards
  to versioning. Never assume the defualt installation of python, for instance, will work with
  your code.
  

### Using an abstracting library (.NET ML, openVino)

Many libraries require you use a specific form of pre-trained model, or they require a different
library for different hardware. This issue is solved by libraries such as ML.NET and openVino that
work to aggregate and abstract libraries and hardware in order to provide a single API for your
AI operations.

**Pros**

- All the pros of using a dedicated library
- No need to have specific version for specific hardware. The libraries will adapt dynamically
- You're able to easily consume a wider range of models
- You're somewhat future proofed against new hardware and model formats 

**Cons**

- An aggregation of libraries and capabilities will result in a larger footprint.
- Abstraction may result in the "least common denominator" issue whereby a library only exposes
  common functionality, meaning you lose access to some features or fine tuning available in a 
  dedicated library
- Your choice of language or platform may be limited. 


## Hosted AI Service

Using a hosted AI service means you do away with all the issues involved with libraries and hardware
and compatible toolkits and dragging around GB of models. You make a call to a hosted AI service
and the result comes back milliseconds later over your low latency, high bandwidth internet
connection. Assuming you have one of those, of course.

The range of services offered by hosted providers is truly amazing. Pre-built models, fast hardware,
great APIs. Just be aware of the cost.

When thinking about the cost you need to understand the charges. Will it cost to upload data to
the provider? What about downloading results? What's the cost pre request and how is it calculated?
Some services will charge per request, some per processing unit, some per time. You also need to
factor in the cost of data storage and any licensing costs that may be applicable. Note also that
the cost will be affected to a high degree by the tasks: passing in data that is applied to a pre-
trained model is one thing, but passing in terrabytes of data for training new models is order of
magnitudes more expensive. GPT-3, for instance, is rumoured to have cost around $5 million to 
train.

There are options to reduce your cost. One method is to mix and match service providers: Upload 
and store your data with a provider such as DELL that has cheap storage. Send this data to Azure, 
which may not have storage ingesting charges, train the model, and send the results back to your
DELL storage. Your data is safe and stored relatively cheaply on one provider, while another 
provider has done the heavy lifting of training your model. Sending data between large hosting 
providers is often extremely fast due to the massive pipes they sit on.

If you are simply using the hosting provider for AI inferencing (ie sending data to an AI model to
have a prediction or analysis made by the model) then you should also be of constraints such as
limits to the absolute number of calls, as well as any throttling that would limit the number of
calls in a given period of time. How will your users react if a piece of functionality disappears
because other users have exhausted your quota for the day?

You also need to understand where the data goes and how the laws in that jurisdiction may affect
you. Will a copy of your data be stored in a foreign jurisdiction? Will your data feed be monitored
or made available to third parties? A webcam feed from inside a person's home may not be something 
a user or your app wants to know is being sent to a foreign country for processing. There may even
be legal or insurance restrrictions you need to be aware of if sending personally identifiable
data outside of your country.

**Pros**

- Fast, powerful, and you get access to the latest and greatest
- No need to worry about AI libraries or versions
- No need to worry about hardware speed or capacity. Your credit card is your only limiter.
- You're able to easily use a wide range of models
- You're future proofed. You'll have access to the latest and greatest
- Your apps will be smaller. No carrying around library code or models
- They will work with any language that can make an API call over HTTP

**Cons**

- You will probably need a decent internet connection to make use of these services
- They can be expensive, or more often than not, ambiguous or opaque in what it will actually cost
- The system is closed. You can't really see what's happening behind the curtains
- You don't control where your data goes. This can be an issue for privacy and security.
- You may face quota or usage issues


## Local AI Service

So what if you don't want to write your own code for AI, you want to use any language or platform
you choose to for your AI analysis, you don't want your data to leave your local network (or even
your machine) and you don't want to pay an unknown amount for a service that you know is available
for free.

A locally hosted AI service such as CodeProject.AI is a great example of an AI service that
is the best of both worlds. You do not need to worry about dealing with libraries and versions,
and any language that can make a HTTP call can interact with the service.

**Pros**

- Local Open Source AI servers can be found online and used for free
- There are no usage limits
- Your data stays where you can see it. Nothing gets sent outside the network unless you choose.
- Like hosted AI services, you don't need to worry about libraries or versioning. It's all within
  the bounds of the service
- You get the benefit of accessing multiple AI features easily without needing to install a library
  or toolkit for each AI operation you want to perform

**Cons**

- The installer for the service may be large depending on what models and features are included.
- Like using a library directly, you are limited by the power of the machine the service is 
  installed on
- Again, like using a library directly, you won't get updates automatically. 
- The AI features offered will not be as large as a hosted service. However, a single service may
  provide multiple AI operations from multiple providers, all presented through a unified API

## Next Steps

Adding AI to an application can fundamentally expand the capabilities of an application while
reducing complexity. Heuristics and hard coded if-then statements get replaced by training sets
based on (hopefully) a wide range of real world data that traditional binary logic can't easily
encompass.

The manner in which you add, AI, has equally fundamental consequences and the choice of how you
do this depends on your requirements, your budget and ultimately your circumstances. 

At CodeProject we have dabbled with all of the methods outlined above in adding AI to our
systems. Our experience has ranged from being pleasantly surprised at how easy some methods 
are, to outright enraged at having to fight the tools every step of the way. 
  
In the end we wanted to share our experience (and the fun) it working with AI to as many developers as 
possible without asking them to go through the frustration. Hunting down compatible libraries,
dealing with models, system tools, paths, oddities between operating systems and oddities between
the same operating system on different CPUs was and still is incredibly time consuming. We built
CodeProject.AI Server as a means to wrap up this complexity into a self contained package that did
all the grunt work for you. Once installed you were immediately at the point were you could start 
playing with, and using, AI in your apps.

[Download the Latest version](https://www.codeproject.com/ai/latest.aspx) and give it a try.


