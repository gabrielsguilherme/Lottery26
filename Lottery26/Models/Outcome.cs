namespace Lottery26.Models;

public class Outcome
{
    public int Id { get; set; } = -1;
    public DateTime Date { get; set; } = DateTime.MinValue;
    public int First { get; set; } = -1;
    public int Second { get; set; } = -1;
    public int Third { get; set; } = -1;
    public int Four { get; set; } = -1;
    public int Five { get; set; } = -1;
    public int Sixth { get; set; } = -1;
}