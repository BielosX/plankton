namespace Plankton.JsonMapping.Tests

open System.Text.Json
open NUnit.Framework
open Plankton.JsonMapping

[<TestFixture>]
type JsonMappingTest() =
    let options = JsonSerializerOptions()

    [<SetUp>]
    member this.SetUp() =
        options.Converters.Add(TupleMapper())

    [<Test>]
    member this.TupleMappterShouldConvertTupleToJsonString() =
        let tuple = (7, 5, 0.2f)
        let result = JsonSerializer.Serialize<int*int*float32>(tuple, options)

        Assert.That(result, Is.EqualTo "[7,5,0.2]")