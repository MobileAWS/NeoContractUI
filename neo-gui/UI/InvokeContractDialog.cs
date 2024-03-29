﻿using Neo.Core;
using Neo.IO.Json;
using Neo.Properties;
using Neo.SmartContract;
using Neo.VM;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Windows.Forms;

namespace Neo.UI
{
    internal partial class InvokeContractDialog : Form
    {
        private InvocationTransaction tx;
        private UInt160 script_hash;
        private ContractParameter[] parameters;

        private static readonly Fixed8 net_fee = Fixed8.FromDecimal(0.001m);

        public InvokeContractDialog(InvocationTransaction tx = null)
        {
            InitializeComponent();
            this.tx = tx;
            if (tx != null)
            {
                tabControl1.SelectedTab = tabPage2;
                textBox6.Text = tx.Script.ToHexString();
            }
        }

        public InvocationTransaction GetTransaction()
        {
            Fixed8 fee = tx.Gas.Equals(Fixed8.Zero) ? net_fee : Fixed8.Zero;
            return Program.CurrentWallet.MakeTransaction(new InvocationTransaction
            {
                Version = tx.Version,
                Script = tx.Script,
                Gas = tx.Gas,
                Attributes = tx.Attributes,
                Inputs = tx.Inputs,
                Outputs = tx.Outputs
            }, fee: fee);
        }

        private void UpdateScript()
        {
            if (parameters.Any(p => p.Value == null)) return;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                foreach (ContractParameter p in parameters) {
                    if (p.Type == ContractParameterType.Array) {
                        var subValues = (IList)p.Value;
                        foreach (ContractParameter subp in subValues) {
                            if (subp.Type == ContractParameterType.Integer && !(subp.Value is BigInteger)) {
                                subp.Value = new BigInteger((long)subp.Value);
                            }
                        }
                    }
                }
                sb.EmitAppCall(script_hash, parameters);
                textBox6.Text = sb.ToArray().ToHexString();
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            button1.Enabled = UInt160.TryParse(textBox1.Text, out _);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            script_hash = UInt160.Parse(textBox1.Text);
            ContractState contract = Blockchain.Default.GetContract(script_hash);
            if (contract == null) return;
            var paramsTypes = contract.ParameterList.Select(p => (byte)p == 22 ? ContractParameterType.Array : p).ToArray();
            parameters = paramsTypes.Select(p => new ContractParameter(p)).ToArray();
            textBox2.Text = contract.Name;
            textBox3.Text = contract.CodeVersion;
            textBox4.Text = contract.Author;
            textBox5.Text = string.Join(", ", paramsTypes);
            button2.Enabled = parameters.Length > 0;
            UpdateScript();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            using (ParametersEditor dialog = new ParametersEditor(parameters))
            {
                dialog.ShowDialog();
            }
            UpdateScript();
        }

        private void textBox6_TextChanged(object sender, EventArgs e)
        {
            button3.Enabled = false;
            button5.Enabled = textBox6.TextLength > 0;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (tx == null) tx = new InvocationTransaction();
            tx.Version = 1;
            tx.Script = textBox6.Text.HexToBytes();
            if (tx.Attributes == null) tx.Attributes = new TransactionAttribute[0];
            if (tx.Inputs == null) tx.Inputs = new CoinReference[0];
            if (tx.Outputs == null) tx.Outputs = new TransactionOutput[0];
            if (tx.Scripts == null) tx.Scripts = new Witness[0];
            ApplicationEngine engine = ApplicationEngine.Run(tx.Script, tx);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"VM State: {engine.State}");
            sb.AppendLine($"Gas Consumed: {engine.GasConsumed}");
            sb.AppendLine($"Evaluation Stack: {new JArray(engine.EvaluationStack.Select(p => p.ToParameter().ToJson()))}");
            textBox7.Text = sb.ToString();
            if (!engine.State.HasFlag(VMState.FAULT))
            {
                tx.Gas = engine.GasConsumed - Fixed8.FromDecimal(10);
                if (tx.Gas < Fixed8.Zero) tx.Gas = Fixed8.Zero;
                tx.Gas = tx.Gas.Ceiling();
                Fixed8 fee = tx.Gas.Equals(Fixed8.Zero) ? net_fee : tx.Gas;
                label7.Text = fee + " gas";
                button3.Enabled = true;
            }
            else
            {
                MessageBox.Show(Strings.ExecutionFailed);
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() != DialogResult.OK) return;
            textBox6.Text = File.ReadAllBytes(openFileDialog1.FileName).ToHexString();
        }
    }
}
