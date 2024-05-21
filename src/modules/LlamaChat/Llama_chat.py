# This model uses the llama_cpp_python library to interact with the LLM.
# See https://llama-cpp_python.readthedocs.io/en/latest/ for more information.

import os
from typing import Iterator, Union

from llama_cpp import ChatCompletionRequestSystemMessage, \
                      ChatCompletionRequestUserMessage,   \
                      CreateCompletionResponse,           \
                      CreateCompletionStreamResponse,     \
                      CreateChatCompletionResponse,       \
                      CreateChatCompletionStreamResponse, \
                      Llama

class LlamaChat:

    def __init__(self, repo_id: str, fileglob:str, filename:str, model_dir:str, 
                 n_ctx: int = 0, n_gpu_layers = -1, verbose: bool = True) -> None:

        try:
            # This will use the model we've already downloaded and cached locally
            self.model_path = os.path.join(model_dir, filename)
            self.llm = Llama(model_path=self.model_path, 
                             n_ctx=n_ctx,
                             n_gpu_layers=n_gpu_layers,
                             verbose=verbose)
        except:
            try:
                # This will download the model from the repo and cache it locally
                # Handy if we didn't download during install
                self.model_path = os.path.join(model_dir, fileglob)
                self.llm        = Llama.from_pretrained(repo_id=repo_id,
                                                        filename=fileglob,
                                                        n_ctx=n_ctx,
                                                        n_gpu_layers=n_gpu_layers,
                                                        verbose=verbose,
                                                        cache_dir=model_dir,
                                                        chat_format="llama-2")
                
                # get the relative path to the model file from the model itself
                self.model_path = os.path.relpath(self.llm.model_path)

            except:
                self.llm        = None
                self.model_path = None

    def do_completion(self, prompt: str, **kwargs) -> \
            Union[CreateCompletionResponse, Iterator[CreateCompletionStreamResponse]]:
        """
        Generates a response from a "complete this" prompt
        params:
            prompt:str	                    The prompt to generate text from.
            suffix: str = None              A suffix to append to the generated text. If None, no suffix is appended.
            max_tokens: int = 128           The maximum number of tokens to generate.
            temperature: float = 0.8        The temperature to use for sampling.
            top_p: float = 0.95             Limit the next token selection to a subset of tokens with a cumulative probability above a threshold P
            top_k: int = 40                 Limit the next token selection to the K most probable tokens
            // min_p: float = 0.95          The minimum probability for a token to be considered, relative to the probability of the most likely token
            // n_predict: int = -1 (infinity) Set the maximum number of tokens to predict when generating text. Note: May exceed the set limit slightly if the last token is a partial multibyte character. When 0, no tokens will be generated but the prompt is evaluated into the cache. 
            // n_keep: Specify the number of tokens from the prompt to retain when the context size is exceeded and tokens need to be discarded. By default, this value is set to 0 (meaning no tokens are kept). Use -1 to retain all tokens from the prompt.
            logprobs: int = None            The number of logprobs to return. If None, no logprobs are returned.
            logits_processor: Optional[LogitsProcessorList] = None
            frequency_penalty: float=0.0 (disable) Repeat alpha frequency penalty
            presence_penalty: float=0.0 (disable) Repeat alpha presence penalty
            repeat_penalty: float = 1.1     Control the repetition of token sequences in the generated text by applying this penalty.
            // penalty_prompt: str = null.  This will replace the prompt for the purpose of the penalty evaluation. Can be either null, a string or an array of numbers representing tokens.
            // typical_p: float = 1.0 (disabled) Enable locally typical sampling with parameter p.
            tfs_z: float = 1.0 (disabled)   Enable tail free sampling with parameter z.
            mirostat (see mirostat_mode)
            mirostat_mode: int = 0          Enable Mirostat sampling, controlling perplexity during text generation (0 = disabled, 1 = Mirostat, 2 = Mirostat 2.0)
            mirostat_tau: float = 5.0       Set the Mirostat target entropy, parameter tau
            mirostat_eta: float = 0.1       Set the Mirostat learning rate, parameter eta
            stopping_criteria = None
            // repeat_last_n: int = 64      Last n tokens to consider for penalizing repetition (0 = disabled, -1 = ctx-size).
            // penalize_nl: bool = True     Penalize newline tokens when applying the repeat penalty.
            // grammar: str = null          Set grammar for grammar-based sampling (default: no grammar)
            // seed: int = -1 (random)      Set the random number generator (RNG) seed
            // ignore_eos: bool = False     Ignore end of stream token and continue generating
            // logit_bias: []               Modify the likelihood of a token appearing in the generated text completion. For example, use "logit_bias": [[15043,1.0]] to increase the likelihood of the token 'Hello', or "logit_bias": [[15043,-1.0]] to decrease its likelihood. Setting the value to false, "logit_bias": [[15043,false]] ensures that the token Hello is never produced.
            // n_probs: int = 0             If greater than 0, the response also contains the probabilities of top N tokens for each generated token
            // system_prompt:               Change the system prompt (initial prompt of all slots), this is useful for chat applications. 
            echo: bool = False              Whether to echo the prompt.
            stream: bool = False            Whether to stream the results.
            stop: [Union[str, List[str]]] = [] A list of strings to stop generation when encountered.  These words will not be included in the completion, so make sure to add them to the prompt for the next iteration 
        """

        completion = self.llm(prompt, **kwargs) if self.llm else None
        return completion

    def do_chat(self, prompt: str, system_prompt: str=None, **kwargs) -> \
            Union[CreateChatCompletionResponse, Iterator[CreateChatCompletionStreamResponse]]:
        """ 
        Generates a response from a chat / conversation prompt
        params:
            prompt:str	                    The prompt to generate text from.
            system_prompt: str=None         The description of the assistant
            max_tokens: int = 128           The maximum number of tokens to generate.
            temperature: float = 0.8        The temperature to use for sampling.
            top_p: float = 0.95             The top-p value to use for sampling.
            top_k: int = 40                 The top-k value to use for sampling.
            logits_processor = None
            frequency_penalty: float=0.0
            presence_penalty: float=0.0
            repeat_penalty: float = 1.1     The penalty to apply to repeated tokens.
            tfs_z: float = 1.0,
            mirostat_mode: int = 0,
            mirostat_tau: float = 5.0,
            mirostat_eta: float = 0.1,
            grammar: Optional[LlamaGrammar] = None
            functions: Optional[List[ChatCompletionFunction]] = None,
            function_call: Optional[Union[str, ChatCompletionFunctionCall]] = None,
            stream: bool = False            Whether to stream the results.
            stop: [Union[str, List[str]]] = [] A list of strings to stop generation when encountered.
        """

        if not system_prompt:
            system_prompt = "You're a helpful assistant who answers questions the user asks of you concisely and accurately."

        completion = self.llm.create_chat_completion(
                        messages=[
                            ChatCompletionRequestSystemMessage(role="system", content=system_prompt),
                            ChatCompletionRequestUserMessage(role="user", content=prompt),
                        ],
                        **kwargs) if self.llm else None

        return completion

    def llm_reset(self):
        self.llm.reset()
 