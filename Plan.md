This is the payment gateway
2 main parts: payment processing and payment detail retrieval

A bank simulator is provided
looks like an api mocker, ejs file contains config


Controller has the GetPaymentAsync function set up
Post function doesn't exist

both the reponses for the GET and POST functions look the same so we can use the same response model

Things I need to do
Create the post request function - done
create validation rules - done
create unit tests for the validation rules - done
create integration with test bank, something like paymentProcessor - done
create paymentProcessor, needs http client - done
make payments repository mockable - done
tests for paymentProcessor - done
tests for PaymentRepository - done
create/fix unit tests for controller - done
other tests - no other tests
integration tests

If time allows
Add resilience to paymentsrepository http client - added retries
add iso currency validation - done
logs - done
traces - done, used ai to help
metrics - default ones from otel should work
health checks - basic one, not sure if custom ones are required here
transaction key so that if a client like the payment machine retries and sends several requests, they will have a guid key attached to the request, and repeated requests are returned early with the cached response - somewhat done



Assumptions made
Stuff like auth, api gateway is outside of the scope
post request is on default endpoint, didn't seem to be mentioned anywhere
get request is on endpoint as shown in existing code, didn't seem to be mentioned
we validated the request parameters, so i changed the type of some of the Bank request and responses as an exercise
bank will supply payment history if we don't have it
clients will request PostPayments with a Unique Key, or we can ask them to


Brain dump
There is nothing here that is representing the actual bank
paymentsrepository looks like a cache for payments, we can use that when integrating with the "bank"
Need separate schema for interacting with bank
I assume PostPaymentRequest.CardNumberLastFour should be just CardNumber, changing that
Why would a payment request only have the last 4? Also the main github page states that this should be Card Number instead of CardNumberLastFour
IValidateObject should work for validating
int doesn't fit a full card number
long might not either
string it is

assuming expiry dates for cards are at the start of the month
Not sure what no more than 3 currency codes means, like no more than 3 characters? or no more than 3 codes are allowed to be matched? that would mean i need to get a list of the codes? I'm assuming no more than 3 characters. I did both
we can add validating against actual iso codes later

added autofixture xunit for the autodata

probably need to handle for when the bank post request fails, return rejected enum

PaymentsProcessor GetPayment should default to repository, then ask bank if not in repository. assumption is that the bank has all data, and we only have a subset
Repository should be updated if response needs to go from bank

should use http client as per good practices

did a bit of clean up like reformatting, getting rid of warnings and issues

fix the program so things are imported correctly

A bunch of small changes here and there after testing

don't think I would usually use a console exported for traces as it clogs up the logs, but will use that here
