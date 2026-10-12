using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chase.Tests
{
    public sealed class ChasePublicFrontendTests
    {
        static ChaseMatch PublicMatch()
        {
            var go=new GameObject("Public front-end test match");
            go.SetActive(false);
            var match=go.AddComponent<ChaseMatch>();
            typeof(ChaseMatch).GetField("showFrontEnd",
                BindingFlags.Instance|BindingFlags.NonPublic).SetValue(match,true);
            go.SetActive(true);
            return match;
        }

        static void DestroyMatch(ChaseMatch m)
        {
            Time.timeScale=1f;
            if(m!=null)Object.DestroyImmediate(m.gameObject);
        }

        [UnityTest] public IEnumerator FrontEndWaitsForExplicitPlaySelection()
        {
            var match=PublicMatch();
            try
            {
                Assert.IsTrue(match.FrontEndEnabled);
                Assert.AreEqual(ChaseMenuPage.MainMenu,match.MenuPage);
                Assert.AreEqual(ChasePhase.Countdown,match.Phase);
                float remaining=match.CountdownRemaining;
                yield return null;
                yield return null;
                Assert.That(match.CountdownRemaining,Is.EqualTo(remaining).Within(.001f),
                    "Public game must not tick a hidden round in the menu");

                match.OpenPlay();
                Assert.AreEqual(ChaseMenuPage.Difficulty,match.MenuPage);
                match.SelectDifficulty(ChaseDifficulty.Rival);
                Assert.AreEqual(ChaseMenuPage.Arena,match.MenuPage);
                Assert.AreEqual(ChaseDifficulty.Rival,match.Difficulty);
                // An arena card is an actual selection, not an immediate
                // difficulty-driven launch.
                Assert.AreEqual(ChasePhase.Countdown,match.Phase);
                match.StartSelectedArena();
                Assert.AreEqual(ChaseMenuPage.InMatch,match.MenuPage);
                Assert.AreEqual(ChasePhase.Countdown,match.Phase);
                Assert.AreEqual(ChaseDifficulty.Rival,
                    match.GetComponent<ChaseBot>().Difficulty);
                Assert.AreEqual(1,match.RoundNumber);
                Assert.AreEqual(0,match.ChaserWins);
                Assert.AreEqual(0,match.EvaderWins);
            }
            finally {DestroyMatch(match);}
        }

        [UnityTest] public IEnumerator MenuBackControlsAndReturnFromPauseAreFunctional()
        {
            var match=PublicMatch();
            int oldHints=PlayerPrefs.GetInt("CHASE_Hints",1);
            int oldKeys=PlayerPrefs.GetInt("CHASE_Highlights",1);
            try
            {
                match.OpenHowTo();
                Assert.AreEqual(ChaseMenuPage.HowTo,match.MenuPage);
                match.MenuBack();
                Assert.AreEqual(ChaseMenuPage.MainMenu,match.MenuPage);
                match.OpenSettings();
                match.SetKeyHints(false);
                match.SetKeyHighlight(false);
                Assert.IsFalse(match.ShowKeyHints);
                Assert.IsFalse(match.HighlightKeys);
                match.MenuBack();
                match.OpenPlay();
                match.SelectDifficulty(ChaseDifficulty.Phantom);
                match.MenuBack();
                Assert.AreEqual(ChaseMenuPage.Difficulty,match.MenuPage);
                match.SelectDifficulty(ChaseDifficulty.Rookie);
                match.StartSelectedArena();

                Assert.AreEqual(ChaseDifficulty.Rookie,
                    match.GetComponent<ChaseBot>().Difficulty);
                var phase=typeof(ChaseMatch).GetField("<Phase>k__BackingField",
                    BindingFlags.Instance|BindingFlags.NonPublic);
                var before=typeof(ChaseMatch).GetField("beforePause",
                    BindingFlags.Instance|BindingFlags.NonPublic);
                before.SetValue(match,ChasePhase.Active);
                phase.SetValue(match,ChasePhase.Paused);
                Time.timeScale=0f;
                match.ResumeMatch();
                Assert.AreEqual(ChasePhase.Active,match.Phase);
                Assert.That(Time.timeScale,Is.EqualTo(1f));
                match.ReturnToMenu();
                Assert.AreEqual(ChaseMenuPage.MainMenu,match.MenuPage);
                match.OpenPlay();
                match.SelectDifficulty(ChaseDifficulty.Runner);
                match.StartSelectedArena();
                Assert.AreEqual(ChasePhase.Countdown,match.Phase);
                Assert.AreEqual(ChaseDifficulty.Runner,
                    match.GetComponent<ChaseBot>().Difficulty);
                yield return null;
            }
            finally
            {
                PlayerPrefs.SetInt("CHASE_Hints",oldHints);
                PlayerPrefs.SetInt("CHASE_Highlights",oldKeys);
                DestroyMatch(match);
            }
        }

        [Test] public void DifficultiesChangeRealBotDecisionsWithoutChangingPhysics()
        {
            var match=PublicMatch();
            try
            {
                ChaseBot bot=match.GetComponent<ChaseBot>();
                var speed=typeof(ChaseBot).GetProperty("MoveFactor",
                    BindingFlags.Instance|BindingFlags.NonPublic);
                var plan=typeof(ChaseBot).GetProperty("ReplanPeriod",
                    BindingFlags.Instance|BindingFlags.NonPublic);
                var prediction=typeof(ChaseBot).GetProperty("Prediction",
                    BindingFlags.Instance|BindingFlags.NonPublic);
                float[] movement=new float[4],period=new float[4],anticipation=new float[4];
                for(int i=0;i<4;i++)
                {
                    bot.Configure((ChaseDifficulty)i);
                    movement[i]=(float)speed.GetValue(bot);
                    period[i]=(float)plan.GetValue(bot);
                    anticipation[i]=(float)prediction.GetValue(bot);
                }
                Assert.Less(movement[0],movement[1]);
                Assert.That(movement[1],Is.EqualTo(1f));
                Assert.That(movement[2],Is.EqualTo(1f));
                Assert.That(movement[3],Is.EqualTo(1f));
                Assert.Greater(period[0],period[1]);
                Assert.Greater(period[1],period[2]);
                Assert.Greater(period[2],period[3]);
                Assert.Less(anticipation[0],anticipation[1]);
                Assert.Less(anticipation[1],anticipation[2]);
                Assert.Less(anticipation[2],anticipation[3]);
            }
            finally {DestroyMatch(match);}
        }

        [UnityTest] public IEnumerator RetryAndNextRoundRetainAndResetCorrectState()
        {
            var match=PublicMatch();
            try
            {
                match.OpenPlay();
                match.SelectDifficulty(ChaseDifficulty.Runner);
                match.StartSelectedArena();
                match.Rematch();
                Assert.AreEqual(1,match.RoundNumber);
                var phase=typeof(ChaseMatch).GetField("<Phase>k__BackingField",
                    BindingFlags.Instance|BindingFlags.NonPublic);
                phase.SetValue(match,ChasePhase.Ended);
                match.NextRound();
                Assert.AreEqual(2,match.RoundNumber);
                Assert.AreEqual(ChasePhase.Countdown,match.Phase);
                phase.SetValue(match,ChasePhase.Ended);
                ChaseRole previous=match.HumanRole;
                match.SwitchRole();
                Assert.AreNotEqual(previous,match.HumanRole);
                Assert.AreEqual(3,match.RoundNumber);
                Assert.AreEqual(ChasePhase.Countdown,match.Phase);
                yield return null;
            }
            finally {DestroyMatch(match);}
        }
    }
}
