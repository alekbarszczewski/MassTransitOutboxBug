# MassTransit Transaction Outbox Bug

* There are two WebApplications (POSTS and USERS) running in same process (listening on two different ports).
* Each application has it's own DI container
* Each application registers it's own DatabaseContext, however they share same database (each context is in separate PG db schema)
* Each application registers it's own MassTransit service with transactional bus outbox
* When started each application logs db queries being made by MassTransit outbox delivery service (each log line contains information from which application it comes from)

Normally outbox delivery service from each web application should only query tables from their own pg db schema
like POSTS should query "posts.outbox_state" and USERS should query "users.outbox_state" because those are
two separate applications with two separate DI containers.

However when running this example:
* sometimes POSTS application queries "users.outbox_state"
* sometimes USERS application queries "posts.outbox_state"
* it only happens for "outbox_state" and not for "inbox_state" - so for example USERS query "posts.outbox_state" and also queries "users.inbox_state"
* it looks like it's configured at the very beginning of application - if this pg schema / outbox_state mismatch occurs it stays like this until application is terminated.
When running application again it's the same or opposite (for example now POSTS is using "users.outbox_state")
* for example app from this repo it happens every time, but I think it might be kind of race condition
* obviously this bug breaks message delivery

Also noticed that if in `ApplicationFactory` file I comment out following code:

```
app.Lifetime.ApplicationStarted.Register(() =>
{
    using var scope = app.Services.CreateScope();
    var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
    var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
    publishEndpoint.Publish(new Message());
    dbContext.SaveChanges();
});
```
Then it works fine and bug does not occur - but it can be like this because of some race condition, not sure.

Example output from console:

```
// USERS queries posts.outbox_state (BUG)
[23:26:40 INF USERS   ] Executed DbCommand (12ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
SELECT m.outbox_id, m.created, m.delivered, m.last_sequence_number, m.lock_id, m.row_version
FROM (
SELECT * FROM "posts"."outbox_state" ORDER BY "created" LIMIT 1 FOR UPDATE SKIP LOCKED
) AS m
LIMIT 2

...

// POSTS queries posts.outbox_state (OK)
[23:56:43 INF POSTS   ] Executed DbCommand (1ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
SELECT m.outbox_id, m.created, m.delivered, m.last_sequence_number, m.lock_id, m.row_version
FROM (
    SELECT * FROM "posts"."outbox_state" ORDER BY "created" LIMIT 1 FOR UPDATE SKIP LOCKED
) AS m
LIMIT 2

...

// POSTS queries posts.inbox_state (OK)
[23:56:52 INF POSTS   ] Executed DbCommand (1ms) [Parameters=[@__removeTimestamp_0='?' (DbType = DateTime), @__p_1='?' (DbType = Int32)], CommandType='Text', CommandTimeout='30']
SELECT i.id, i.consumed, i.consumer_id, i.delivered, i.expiration_time, i.last_sequence_number, i.lock_id, i.message_id, i.receive_count, i.received, i.row_version
FROM posts.inbox_state AS i
WHERE i.delivered IS NOT NULL AND i.delivered < @__removeTimestamp_0
ORDER BY i.delivered
LIMIT @__p_1

...

// USERS queries users.inbox_state (OK)
[23:56:52 INF USERS   ] Executed DbCommand (2ms) [Parameters=[@__removeTimestamp_0='?' (DbType = DateTime), @__p_1='?' (DbType = Int32)], CommandType='Text', CommandTimeout='30']
SELECT i.id, i.consumed, i.consumer_id, i.delivered, i.expiration_time, i.last_sequence_number, i.lock_id, i.message_id, i.receive_count, i.received, i.row_version
FROM users.inbox_state AS i
WHERE i.delivered IS NOT NULL AND i.delivered < @__removeTimestamp_0
ORDER BY i.delivered
LIMIT @__p_1

```

# Running this example
1. Use `docker-compose up` to run Postgres (port 5501) and RabbitMQ (port 5701)
2. Use `cd App && dotnet build && dotnet run` to run this example (app will listen on ports 5000 and 5001)
3. Wait couple of seconds for logs from outbox delivery service to appear