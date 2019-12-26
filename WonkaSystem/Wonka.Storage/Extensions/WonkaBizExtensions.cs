﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Wonka.BizRulesEngine;
using Wonka.BizRulesEngine.RuleTree;
using Wonka.BizRulesEngine.RuleTree.RuleTypes;
using Wonka.Eth.Extensions;
using Wonka.MetaData;
using Wonka.Product;

using Wonka.Storage.Rules;

namespace Wonka.Storage.Extensions
{
    public static class WonkaBizExtensions
    {
        /// <summary>
        /// 
        /// This method will create a custom operator rule that will determine whether an attribute's value falls within a domain defined by the
        /// results of a query on a database.
        /// 
        /// NOTE: UNDER CONSTRUCTION
        /// 
        /// </summary>
        public static CustomOperatorRule BuildSqlQueryRule(this WonkaBizSource poSource, int pnRuleID, bool pbQueryDomainRule = false)
        {
            var QueryRule = new WonkaBizQueryRule(pnRuleID, poSource) { IsDomainQuery = pbQueryDomainRule };

            return QueryRule;
        }

        /// <summary>
        /// 
        /// This method will create a custom operator rule that will determine whether an attribute's value falls within a domain defined by the
        /// results of a query on a database.
        /// 
        /// NOTE: UNDER CONSTRUCTION
        /// 
        /// </summary>
        public static CustomOperatorRule BuildSqlQueryRule(this WonkaBizSource poSource, int pnRuleID, int pnTargetAttrId, string psCustomOpName, bool pbQueryDomainRule = false)
        {
            var QueryRule = new WonkaBizQueryRule(pnRuleID, pnTargetAttrId, psCustomOpName, poSource) { IsDomainQuery = pbQueryDomainRule };

            return QueryRule;
        }
    }
}