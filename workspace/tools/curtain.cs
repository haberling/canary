string? line;
while ((line = Console.In.ReadLine()) is not null)
{
    Console.Out.WriteLine(line);
}
Console.Out.WriteLine();
Console.Out.WriteLine("*-- Curtain. --*");
