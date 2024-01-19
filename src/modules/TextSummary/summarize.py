#!/usr/bin/env python
# coding: utf-8

from typing import List
from nltk.corpus import stopwords
from nltk.cluster.util import cosine_distance
import numpy as np
import networkx as nx

#nltk.download('stopwords')

class Summarize:

    def read_article(self, file_name: str):

        # Read all lines into an array
        try:
            file = open(file_name, "r")
            filedata = file.readlines()
        except:
            print("Unable to load file")
            return ""

        # Join the lines together
        allLines = ' '.join(filedata)

        # and then break into sentences. A line in a file may contain multiple sentences
        # TODO: handle abbreviations such as U.S. and Inc.
        article = allLines.split(". ")

        sentences = []
        for sentence in article:
            # print(sentence)
            sentences.append(sentence.replace("[^a-zA-Z]", " ").split(" "))

        sentences.pop()

        return sentences

    def sentence_similarity(self, sent1:str, sent2:str):

        sent1 = [w.lower() for w in sent1]
        sent2 = [w.lower() for w in sent2]

        all_words = list(set(sent1 + sent2))

        vector1 = [0] * len(all_words)
        vector2 = [0] * len(all_words)

        # build the vector for the first sentence
        for w in sent1:
            vector1[all_words.index(w)] += 1

        # build the vector for the second sentence
        for w in sent2:
            vector2[all_words.index(w)] += 1

        return 1 - cosine_distance(vector1, vector2)

    def remove_stop_words(self, sentences: List, stopwords: List = None):

        if stopwords is None:
            stopwords = []

        stripped_sentences = []
        for sentence in sentences:
            stripped_sentence = []

            for word in sentence:
                # we want to ignore case when comparing the sentences
                lcword = word.lower()
                # and we will ignore any stop words
                if lcword in stopwords:
                    continue

                stripped_sentence.append(lcword)

            stripped_sentences.append(stripped_sentence)

        return stripped_sentences

    def build_similarity_matrix(self, sentences: List, stop_words: List):

        # Remove the stop words once so we don't have to check
        # when evaluating each sentence multiple times.
        stripped_sentences = self.remove_stop_words(sentences, stop_words)

        # Create an empty similarity matrix
        similarity_matrix = np.zeros((len(stripped_sentences), len(stripped_sentences)))

        # Optimize calculation as similarity(a,b) == similarity(b,a)
        for idx1 in range(len(stripped_sentences)):
            for idx2 in range(idx1, len(stripped_sentences)):
                if idx1 == idx2: #ignore if both are same sentences
                    continue

                similarity = self.sentence_similarity(stripped_sentences[idx1],stripped_sentences[idx2])
                similarity_matrix[idx1][idx2] = similarity
                similarity_matrix[idx2][idx1] = similarity

        return similarity_matrix


    def generate_summary(self, sentences: List, top_n: int = 5):

        if (not sentences or len(sentences) == 0):
            print("No sentences provided to generate_summary()\n")
            return ""

        stop_words = stopwords.words('english')

        summarize_text = []

        # Step 2 - Generate Similary Martix across sentences
        sentence_similarity_martix = self.build_similarity_matrix(sentences, stop_words)

        # Step 3 - Rank sentences in similarity martix
        sentence_similarity_graph = nx.from_numpy_array(sentence_similarity_martix)
        scores = nx.pagerank(sentence_similarity_graph)

        # Step 4 - Sort the rank and pick top sentences. Result is array of [rank, sentence]
        ranked_sentence = sorted(((scores[i],s) for i,s in enumerate(sentences)), reverse=True)
        #print("Indexes of top ranked_sentence order are ", ranked_sentence)

        for i in range(top_n):
            if len(ranked_sentence[i]) > 0:
                summarize_text.append(" ".join(ranked_sentence[i][1]))

        # Step 5 - Output the summarize text
        summary = ". ".join(summarize_text)
        # print("Summarize Text: \n", summary)

        return summary


    def generate_summary_from_file(self, file_name: str, top_n: int = 5):

        # Step 1 - Read file and split it into sentences
        sentences = self.read_article(file_name)
        return self.generate_summary(sentences, top_n)


    def generate_summary_from_text(self, text: str, top_n: int = 5):

        if (not text or text.isspace()):
            print("No text provided to generate_summary_from_text()\n")
            return ""

        sentences = []

        # Step 1 - Split text into paragraphs
        paragraphs = text.split("\n")

        # Step 2 - Split paragraphs into sentences
        for paragraph in paragraphs:
            sublines = paragraph.split(". ") # sentences, really.
            for subline in sublines:
                # print(subline)
                sentence = subline.replace("[^a-zA-Z]", " ").split(" ")
                if len(sentence) > 0:
                    sentences.append(sentence)

        print("Number of sentences = ", len(sentences))

        return self.generate_summary(sentences, top_n)
