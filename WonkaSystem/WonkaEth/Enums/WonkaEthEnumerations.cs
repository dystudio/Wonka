﻿namespace Wonka.Eth.Enums
{
    public enum GAS_COST
    {
		CONST_GAS_PER_READ_OP = 80000,
        CONST_GAS_PER_WRITE_OP = 125000,
        CONST_MIN_OP_GAS_COST_DEFAULT = 125000,
        CONST_MID_OP_GAS_COST_DEFAULT = 1000000,
        CONST_MAX_OP_GAS_COST_DEFAULT = 2000000,
        CONST_DEPLOY_DEFAULT_CONTRACT_GAS_COST = 2000000,
        CONST_DEPLOY_ENGINE_CONTRACT_GAS_COST = 8388608,
        CONST_GAS_COST_MAX = 10000000
    }
}
