module ApiTests

open System
open System.Security.Cryptography

open Fuchu

open EventStore.ClientAPI


[<Tests>]
let utilities =
  testList "utilities unit tests" [

    testCase "Option.fromNullable null" <| fun _ ->
      let n : Nullable<int> = Nullable()
      Assert.Equal("should be None", None, Helpers.Option.fromNullable n)
    testCase "Option.fromNullable not null" <| fun _ ->
      let n : Nullable<int> = Nullable 4
      Assert.Equal("should be Some 4", Some 4, Helpers.Option.fromNullable n)

    testCase "Helpers.memoize" <| fun _ ->
      let value = ref 0
      let f : unit -> int =
        fun () ->
          value := !value + 1
          !value
      let f' = Helpers.memoize f
      Assert.Equal("should be one after first call",
                   1, f' ())
      Assert.Equal("should be one after second call, too",
                   1, f' ())
      Assert.Equal("but calling the original function increments",
                   2, f ())
    ]


open Events

type A(y) =
  member x.X = y

[<Tests>]
let units =
  testList "eventstore client api unit test" [
    testCase "empty EventData" <| fun _ ->
      EventData.Empty |> ignore
    testCase "empty RecordedEvent" <| fun _ ->
      RecordedEvent.Empty |> ignore
    testCase "EventData from RecordedEvent" <| fun _ ->
      EventData.FromRecordedEvent true RecordedEvent.Empty |> ignore
    testCase "RecordedEvent from EventData" <| fun _ ->
      RecordedEvent.FromEventData "stream id" 1234u EventData.Empty
      |> ignore
    testCase "empty ResolvedEvent" <| fun _ ->
      ResolvedEvent.Empty |> ignore
    testCase "empty EventReadResult" <| fun _ ->
      EventReadResult.Empty |> ignore
    testCase "empty StreamEventsSlice" <| fun _ ->
      StreamEventsSlice.Empty |> ignore
    testCase "empty AllEventsSlice" <| fun _ ->
      AllEventsSlice.Empty |> ignore
    testCase "clr type name from type" <| fun _ ->
      let t = typeof<A>
      Assert.Equal("should eq module plus type name",
                   "ApiTests+A, EventStore.ClientAPI.FSharp.Tests",
                   t.ToPartiallyQualifiedName ())
    testCase "compute sha1 on empty array" <| fun _ ->
      use sha = SHA1.Create()
      Assert.Equal("160 bits size", 160, sha.HashSize)
      let subject = sha.ComputeHash [||]
      Assert.Equal("should be 160 bits", 160, subject.Length * 8)
      Assert.NotEqual("hash of empty array is not zero(oes)",
                      [||], subject)
    testCase "hash a string into a guid" <| fun _ ->
      let subject = "Hello World, of course!"
      let guid    = to_guid subject
      Assert.NotEqual("not empty", Guid.Empty, guid)
    testCase "can create EventData from some object" <| fun _ ->
      let data = A 42
      let subject = EventData.From data "EventA"
      Assert.Equal("should have id from serialized object",
                   data |> to_json |> to_guid,
                   subject.Id)
      Assert.Equal("should have type that was passed",
                   "EventA", subject.Type)
      Assert.Equal("should have json serialized data",
                   data |> to_jsonb,
                   subject.Data)
      Assert.Equal("should say it's JSON",
                   true, subject.IsJson)
  ]

open EventStore.ClientAPI.Embedded
open EventStore.Core
open System.Net
open Intelliplan.JsonNet

let with_eventstore f =
  let node = EmbeddedVNodeBuilder.AsSingleNode()
                                  .OnDefaultEndpoints()
                                  .RunInMemory()
                                  .RunProjections(ProjectionsMode.All)
                                  .WithWorkerThreads(16)
                                  .Build()
  node.Start()
  f()
  node.Stop()

type Stomach = { beers_downed : int }
type Drink =
  | DrinkABeer
type Cmd =
  | OrderABeer
  | OrderThreeBeers

let empty = { beers_downed = 0 }
let apply (s : Stomach) = function
  | DrinkABeer -> s
let exec (s : Stomach) = function 
  | CmdT cmd -> [ EventT { id = cmd.id} ]

let aggregate =
  { Aggregate.zero  = empty
    Aggregate.apply = apply
    Aggregate.exec  = exec }

let ser, deser = Serialisation.serialise, Serialisation.deserialise

let test_write conn test_id =
  let load, commit = Repo.make conn ser deser
  let handler = Aggregate.makeHandler aggregate load commit
  let version = NoStream
  async {
    let! events, write = handler ("test_stream", version)
                                 (CmdT({ id = test_id}))
    do! write
    return events }


[<Tests>]
let aggregates =
  testList "aggregates unit tests"  [
    testCase "should connect and disconnect" <| fun _ ->
      with_eventstore <| fun _ ->
        let conn = Conn.configureStart()
                    |> Conn.configureEnd (IPEndPoint(IPAddress.Loopback, 1113))
        conn
        |> Conn.connect
        |> Async.RunSynchronously
        conn
        |> Conn.close
    testCase "read and write to stream" <| fun _ ->
      with_eventstore <| fun _ ->
        let conn = Conn.configureStart()
                    |> Conn.configureEnd (IPEndPoint(IPAddress.Loopback, 1113))
        
        
        test_write conn "id_1"
        |> Async.RunSynchronously
        |> ignore

        test_write conn "id_2"
        |> Async.RunSynchronously
        |> ignore
        // skriv tv[ CMds
        // ASssert
                    
      
  ]


