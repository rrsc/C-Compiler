﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class LexTest {
    [TestMethod]
    public void test_lex() {
        LexicalAnalysis lex = new LexicalAnalysis();
        lex.src = "int main() { return 0; }";
        lex.Lex();
        string output = lex.ToString();
    }
}
