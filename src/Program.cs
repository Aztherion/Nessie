namespace Nessie
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var nesGame = new NesGame())
            {
                nesGame.Run();
            }
        }
    }
}
