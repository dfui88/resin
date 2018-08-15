﻿using System.Linq;

namespace Sir.Store
{
    /// <summary>
    /// Parses key:value clauses where keys are separated from values by a ':' and
    /// clauses are separated by newline characters. 
    /// Clauses may be appended with a + sign (meaning AND), a - sign (meaning NOT) or nothing (meaning OR).
    /// </summary>
    public class BooleanKeyValueQueryParser : IQueryParser
    {
        public string ContentType => "*";
        private static  char[] Operators = new char[] { ' ', '+', '-' };

        public Query Parse(string query, ITokenizer tokenizer)
        {
            Query root = null;
            Query previous = null;
            var lines = query.Split('\n');

            foreach (var line in lines)
            {
                var parts = line.Split(':');
                var key = parts[0];
                var value = parts[1];

                var values = (key[0] == '_' || tokenizer == null) ?
                    new[] { tokenizer.Normalize(value) } : tokenizer.Tokenize(value);

                var and = root == null || key[0] == '+';
                var not = key[0] == '-';
                var or = !and && !not;

                if (Operators.Contains(key[0]))
                {
                    key = key.Substring(1);
                }

                foreach (var val in values)
                {
                    var q = new Query { Term = new Term(key, val), And = and, Not = not, Or = or };

                    if (previous == null)
                    {
                        root = q;
                        previous = q;
                    }
                    else
                    {
                        previous.Next = q;
                        previous = q;
                    }
                }
            }
            
            return root;
        }
        
        public void Dispose()
        {
        }
    }
}
