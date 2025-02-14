namespace Plankton.JsonMapping.Tests

open System.Text.Json
open NUnit.Framework
open Plankton.JsonMapping

type TestRecord = { first: string; second: int; third: int }
type SecondTestRecord = { first: TestRecord; second: int }
type TestUnion =
    | FirstCase of first: string * second: int
    | SecondCase of third: int * fourth: (int*int)

[<TestFixture>]
type JsonMappingTest() =
    let options = JsonSerializerOptions()

    [<OneTimeSetUp>]
    member this.SetUp() =
        options.Converters.Add(TupleMapper())
        options.Converters.Add(RecordMapper())
        options.Converters.Add(UnionMapper())

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

    [<Test>]
    member this.RecordMapperShouldConvertRecordToJsonString() =
        let record: TestRecord = { first = "test"; second = 2; third = 7 }
        let result = JsonSerializer.Serialize<TestRecord>(record, options)

        let expected = "{\"first\":\"test\",\"second\":2,\"third\":7}"
        Assert.That(result, Is.EqualTo expected)

    [<Test>]
    member this.RecordMapperShouldConvertJsonStringToRecord() =
        let str = """
            {
                "first": {
                    "second": 2,
                    "third": 7,
                    "first": "test"
                },
                "second": 9
            }
        """
        let result = JsonSerializer.Deserialize<SecondTestRecord>(str, options)

        let expected: SecondTestRecord = {first = {first = "test"; second = 2; third = 7}; second = 9}
        Assert.That(result, Is.EqualTo expected)

    [<Test>]
    member this.UnionMapperShouldConvertUnionToJsonString() =
        let firstUnion = FirstCase(first = "asdf", second = 5)
        let secondUnion = SecondCase(third = 7, fourth = (1, 2))

        let firstResult = JsonSerializer.Serialize<TestUnion>(firstUnion, options)
        let secondResult = JsonSerializer.Serialize<TestUnion>(secondUnion, options)

        let firstExpected = "{\"FirstCase\":{\"first\":\"asdf\",\"second\":5}}"
        let secondExpected = "{\"SecondCase\":{\"third\":7,\"fourth\":[1,2]}}"

        Assert.That(firstResult, Is.EqualTo firstExpected)
        Assert.That(secondResult, Is.EqualTo secondExpected)

    [<Test>]
    member this.UnionMapperShouldConvertJsonStringToUnion() =
        let str = """
            {
                "FirstCase": {
                    "first": "test",
                    "second": 7
                }
            }
        """

        let result = this.deserialize<TestUnion> str
        let expected = FirstCase(first = "test", second = 7)

        Assert.That(result, Is.EqualTo expected)