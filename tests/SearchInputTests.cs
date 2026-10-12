using System;
using System.Collections.Generic;
namespace FocusSearch.Tests
{
    internal static class SearchInputTests
    {
        public static void Register(ICollection<Action> tests) { tests.Add(NativeTextMessagesDoNotRecurse); }
        private static void NativeTextMessagesDoNotRecurse()
        {
            using (var input = new SearchInput())
            {
                var handle = input.Handle;
                TestRunner.Equal(0, input.TextLength, "Empty native edit must report length without recursive messages.");
                input.Text = "test";
                TestRunner.Equal(4, input.TextLength, "Native text messages must remain functional.");
            }
        }
    }
}
