using System;

namespace SnmpSharpNet;

class Block512
{
    // A Block512 consists of an array of 16 elements of type uint.
    public uint[]? Words;

    public Block512(uint[] words)
    {
        if (words.Length == 16)
        {
            Words = words;
        }
        else
        {
            Console.WriteLine("ERROR: A block must be 16 words");
            Words = null;
        }
    }
}