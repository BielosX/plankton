namespace Plankton.JsonMapping.Tests

open System.Text.Json
open NUnit.Framework
open Plankton.JsonMapping

[<TestFixture>]
type JsonMappingTest() =
    let options = JsonSerializerOptions()

    [<OneTimeSetUp>]
    member this.SetUp() =
        options.Converters.Add(TupleMapper())

    member this.deserialize<'a when 'a: not null and 'a: not struct>(json: string): 'a =
        nonNull (JsonSerializer.Deserialize<'a>(json, options))

    [<Test>]
    member this.TupleMappterShouldConvertTupleToJsonString() =
        let tuple = (7, 5, 0.2f)
        let result = JsonSerializer.Serialize<int*int*float32>(tuple, options)

        Assert.That(result, Is.EqualTo "[7,5,0.2]")

    [<Test>]
    member this.TupleMapperShouldConvertJsonStringToTuple() =
        let str = "[1,2,3]"
        let result = this.deserialize<int*int*int> str

        Assert.That(result, Is.EqualTo((1, 2, 3)))