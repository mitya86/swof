var options = Swof.Game.SwofGameLaunchOptions.Parse(args);
using var game = new Swof.Game.SwofGame(options);
game.Run();
